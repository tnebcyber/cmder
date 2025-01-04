using FluentCMS.CoreKit.ApiClient;
using FluentCMS.Utils.JsonElementExt;
using FluentCMS.Core.Descriptors;
using FluentCMS.Utils.ResultExt;
using FluentCMS.CoreKit.Test;
using IdGen;
using Microsoft.Extensions.Primitives;

namespace FluentCMS.Blog.Tests;

public class QueryApiTest
{
    private readonly string _post = "post" + new IdGenerator(0).CreateId();
    private readonly QueryApiClient _query;
    private readonly BlogsTestCases _commonTestCases;


    public QueryApiTest()
    {
        Util.SetTestConnectionString();

        WebAppClient<Program> webAppClient = new();
        var entityClient = new EntityApiClient(webAppClient.GetHttpClient());
        var schemaClient = new SchemaApiClient(webAppClient.GetHttpClient());
        var accountApiClient = new AccountApiClient(webAppClient.GetHttpClient());

        _query = new QueryApiClient(webAppClient.GetHttpClient());
        _commonTestCases = new BlogsTestCases(_query, _post);


        accountApiClient.EnsureLogin().Wait();
        if (schemaClient.ExistsEntity("post").GetAwaiter().GetResult()) return;
        BlogsTestData.EnsureBlogEntities(schemaClient).Wait();
        BlogsTestData.PopulateData(entityClient).Wait();
    }



    [Fact]
    public Task VerifyValueSetMatch() => _commonTestCases.Filter.ValueSetMatch();
    [Fact]
    public Task VerifyMatchAllCondition() => _commonTestCases.Filter.MatchAllCondition();
    [Fact]
    public Task VerifyMatchAnyCondition() => _commonTestCases.Filter.MatchAnyCondition();
    [Fact]
    public Task VerifyFilterExpression() => _commonTestCases.Filter.VerifyFilterExpression();




    [Fact]
    public Task VerifyRecordCount() => _commonTestCases.SavedQuery.VerifyRecordCount();

    [Fact]
    public Task PaginationByCurosr() => _commonTestCases.SavedQuery.PaginationByCursor();

    [Fact]
    public Task VerifyManyApi() => _commonTestCases.SavedQuery.VerifyManyApi();

    [Fact]
    public Task VerifySingleApi() => _commonTestCases.SavedQuery.VerifySingleApi();

    
    [Fact]
    public Task VerifySort() => _commonTestCases.Sort.VerifySort();

    [Fact]
    public Task VerifySortExpression() => _commonTestCases.Sort.VerifySortExpression();


    [Fact]
    public Task VerifySingleGraphQlQuery() => _commonTestCases.RealtimeQueryTest.SingleGraphQlQuery();

    [Fact]
    public Task VerifyComplexFieldSelection() => _commonTestCases.RealtimeQueryTest.ComplexFieldSelection();
    
    [Fact]
    public Task VerifyPagination() => _commonTestCases.RealtimeQueryTest.RealtimeQueryPagination();

    [Fact]
    public Task VariableInSet() => _commonTestCases.Variable.ValueInSet();

    [Fact]
    public Task VariableStartsWith() => _commonTestCases.Variable.StartsWith();

    [Fact]
    public Task VariableFilterExpression() => _commonTestCases.Variable.FilterExpression();
    
    [Fact]
    public Task VariableSort() => _commonTestCases.Variable.Sort();
    [Fact]
    public Task VariableSortExpression() => _commonTestCases.Variable.SortExpression();
    [Fact]
    public Task VariablePagination() => _commonTestCases.Variable.Pagination();
    
    
    
    
    [Fact]
    public Task CollectionPart() => QueryParts("attachments", ["id", "post"]);

    [Fact]
    public Task JunctionPart() => QueryParts("tags", ["id"]);
    


    private async Task QueryParts(string attrName, string[] attrs)
    {
        const int limit = 4;
        await $$"""
                query {{_post}}{
                   postList{
                      id
                      {{attrName}}{
                          {{string.Join(",", attrs)}}
                      }
                   }
                }
                """.GraphQlQuery(_query).Ok();

        var posts = (await _query.ListArgs(_post, new Dictionary<string, StringValues>
        {
            [$"{attrName}.limit"] = limit.ToString(),
            [$"limit"] = "1",
        })).Ok();

        var post = posts[0].ToDictionary();
        if (post.TryGetValue(attrName, out var v)
            && v is object[] arr
            && arr.Last() is Dictionary<string, object> last)
        {

            var cursor = SpanHelper.Cursor(last);
            var items = (await _query.Part(query: _post, attr: attrName, last: cursor, limit: limit)).Ok();
            Assert.Equal(limit, items.Length);
        }
        else
        {
            Assert.Fail("Failed to find last cursor");
        }
    }
}