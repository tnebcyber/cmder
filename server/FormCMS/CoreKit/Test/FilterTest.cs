using System.Text.Json;
using FormCMS.Utils.ResultExt;
using FormCMS.CoreKit.ApiClient;

namespace FormCMS.CoreKit.Test;

public class FilterTest(QueryApiClient client, string queryName)
{
    public async Task ValueSetMatch()
    {
        var item = await $$"""
                           query {{queryName}}{ post(idSet:1){ id } }
                           """.GraphQlQuery<JsonElement>(client).Ok();
        SimpleAssert.IsTrue(item.HasId());
        item = await $$"""
                       query {{queryName}}{ post(idSet:[1]){ id } }
                       """.GraphQlQuery<JsonElement>(client).Ok();
        SimpleAssert.IsTrue(item.HasId());
    }


    public async Task MatchAllCondition()
    {
        var e = await $$"""
                        query {{queryName}}{
                            post(
                              id: {matchType: matchAll, gt: 1, lt: 3}
                            ){
                              id
                            }
                        }
                        """.GraphQlQuery<JsonElement>(client).Ok();
        SimpleAssert.AreEqual(2, e.Id());
    }

    public async Task MatchAnyCondition()
    {
        var e = await $$"""
                        query {{queryName}}{
                            postList(
                              sort:id,
                              title: [{matchType: matchAny}, {startsWith:"title-99"}, {startsWith:"title-98"}]
                            ){
                              id,title
                            }
                        }
                        """.GraphQlQuery<JsonElement[]>(client).Ok();
        SimpleAssert.AreEqual(98, e[0].Id());
        SimpleAssert.AreEqual(99, e[1].Id());
    }
    
    public async Task VerifyFilterExpression()
    {
        var items = await $$"""
                            query {{queryName}}{
                              postList(
                                filterExpr:[
                                  {
                                    field:"authors.name",
                                    clause:[{
                                      startsWith:"name"
                                    }]
                                  }
                                ]
                              ){
                                id
                              }
                            }
                            """.GraphQlQuery<JsonElement[]>(client).Ok();
        SimpleAssert.AreNotEqual(0, items.Length);
    }
}