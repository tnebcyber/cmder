using FluentCMS.CoreKit.ApiClient;
using FluentCMS.Utils.JsonElementExt;
using FluentCMS.Utils.ResultExt;

namespace FluentCMS.Blog.DistributeTest;

public class DistributeTest
{
    private const string Post = "distribut_test_post";
    private const string Title = "title";
    
    private const string Addr1 = "http://localhost:5134";
    private const string Addr2 = "http://localhost:5135";

    private readonly SchemaApiClient _leaderSchema;
    private readonly EntityApiClient _leaderEntity;
    private readonly QueryApiClient _leaderQuery;
    private readonly AccountApiClient _leaderAccount;
    
    private readonly QueryApiClient _followerQuery;

    public DistributeTest()
    {
        var httpClient1 = new HttpClient
        {
            BaseAddress = new Uri(Addr1)
        };
        var httpClient2 = new HttpClient
        {
            BaseAddress = new Uri(Addr2)
        };
         
        _leaderAccount = new AccountApiClient(httpClient1);
        _leaderSchema = new SchemaApiClient(httpClient1);
        _leaderEntity = new EntityApiClient(httpClient1);
        _leaderQuery = new QueryApiClient(httpClient1);
        
        _followerQuery = new QueryApiClient(httpClient2);
    }

    string EntityName()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return Post + suffix;
        
    }
    [Fact]
    public async Task EntityChange()
    {
        (await _leaderAccount.EnsureLogin()).Ok();
        
        var entityName = EntityName();
        var schema = (await _leaderSchema.EnsureSimpleEntity(entityName, Title)).Ok();
        await _leaderEntity.Insert(entityName, Title,"title1");
        
        Thread.Sleep(TimeSpan.FromSeconds(20));
        (await _followerQuery.SingleGraphQl(entityName, ["id",Title])).Ok();
        (await _leaderSchema.Delete(schema.Id)).Ok();
        Thread.Sleep(TimeSpan.FromSeconds(20)); 
        (await _followerQuery.SingleGraphQl(entityName, [Title])).Ok();
    }

    [Fact]
    public async Task QueryChange()
    {
        (await _leaderAccount.EnsureLogin()).Ok();

        var entityName = EntityName();
        (await _leaderSchema.EnsureSimpleEntity(entityName, Title)).Ok();
        await _leaderEntity.Insert(entityName, Title, "title1");
        
        (await _leaderQuery.SingleGraphQl(entityName, ["id"])).Ok();
        Thread.Sleep(TimeSpan.FromSeconds(20));
        var result = (await _followerQuery.List(entityName)).Ok();
        Assert.Equal(4,result.First().ToDictionary().Count);
        
        (await _leaderQuery.SingleGraphQl(entityName, ["id", Title])).Ok();
        Thread.Sleep(TimeSpan.FromSeconds(20));
        result =(await _followerQuery.List(entityName)).Ok();
        Assert.Equal(5, result.First().ToDictionary().Count);
    }
}