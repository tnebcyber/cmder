using System.Text.Json;
using FluentCMS.Utils.QueryBuilder;

namespace FluentCMS.Cms.Services;

public interface IEntityService
{
    Task<ListResponse?> ListWithAction(string name,ListResponseMode mode, Pagination pagination,  StrArgs args,
        CancellationToken ct= default);
    Task<Record> SingleWithAction(string entityName, string strId, CancellationToken ct = default);
    Task<Record> SingleByIdBasic(string entityName, string strId, string[]attributes, CancellationToken ct =default);
    
    Task<Record> InsertWithAction(string name, JsonElement item, CancellationToken ct = default);
    Task BatchInsert(string tableName,Record[] items);
    Task<Record> UpdateWithAction(string name, JsonElement item, CancellationToken ct= default);
    Task<Record> DeleteWithAction(string name, JsonElement item, CancellationToken ct= default);

    Task<ListResponse> CollectionList(string name, string id, string attr, Pagination pagination,  StrArgs args, CancellationToken ct = default);
    Task<Record> CollectionInsert(string name, string id, string attr, JsonElement element, CancellationToken ct = default);
    
    Task<ListResponse> JunctionList(string name, string id, string attr, bool exclude, Pagination pagination,StrArgs args,  CancellationToken ct= default);
    Task<int> JunctionSave(string name, string id, string attr, JsonElement[] elements, CancellationToken ct = default);
    Task<int> JunctionDelete(string name, string id, string attr, JsonElement[] elements, CancellationToken ct= default);

    Task<LookupListResponse> LookupList(string name, string startsVal, CancellationToken ct = default);
}