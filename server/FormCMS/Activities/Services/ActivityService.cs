using FormCMS.Activities.Models;
using FormCMS.Cms.Services;
using FormCMS.Core.Descriptors;
using FormCMS.Infrastructure.Buffers;
using FormCMS.Infrastructure.Cache;
using FormCMS.Infrastructure.RelationDbDao;
using FormCMS.Utils.ResultExt;

namespace FormCMS.Activities.Services;

public class ActivityService(
    ICountBuffer countBuffer,
    IStatusBuffer statusBuffer,
    KeyValueCache<long> maxRecordIdCache,
        
    ActivitySettings settings,
    IProfileService profileService,
    IEntitySchemaService schemaService,  
    IQueryService queryService,
    
    
    IRelationDbDao dao,
    DatabaseMigrator migrator
) : IActivityService
{
    public async Task<Activity> GetActivityItems(string activityType, CancellationToken ct = default)
    {
       // var activities = await GetStatusDict()
       throw new NotImplementedException();
    }
    
    public async Task EnsureActivityTables()
    {
        await migrator.MigrateTable(Models.Activities.TableName, Models.Activities.Columns);
        await dao.CreateIndex(Models.Activities.TableName, Models.Activities.KeyFields, true, CancellationToken.None);

        await migrator.MigrateTable(ActivityCounts.TableName, ActivityCounts.Columns);
        await dao.CreateIndex(ActivityCounts.TableName, ActivityCounts.KeyFields, true, CancellationToken.None);
    }

    public async Task Flush(DateTime? lastFlushTime, CancellationToken ct)
    {
        if (!settings.EnableBuffering)
            return;

        lastFlushTime ??= DateTime.UtcNow.AddMinutes(-1);

        var counts = await countBuffer.GetAfterLastFlush(lastFlushTime.Value);
        var countRecords = counts.Select(pair =>
            (ActivityCounts.Parse(pair.Key) with { Count = pair.Value }).UpsertRecord()).ToArray();
        
        await dao.BatchUpdateOnConflict(ActivityCounts.TableName,  countRecords, ActivityCounts.KeyFields,ct);
        
        //Query title and image 
        var statusList = await statusBuffer.GetAfterLastFlush(lastFlushTime.Value);
        var activities = statusList.Select(pair => Models.Activities.Parse(pair.Key) with { IsActive = pair.Value }).ToArray();
        await UpsertActivities(activities,ct);
    }
    
    public async Task<Dictionary<string, StatusDto>> Get(string entityName, long recordId, CancellationToken ct)
    {
        await EnsureEntityRecordExists(entityName, recordId,ct);
        var ret = new Dictionary<string, StatusDto>();
        foreach (var pair in await Record(entityName, recordId, settings.AutoRecordActivities.ToArray(), ct))
        {
            ret[pair.Key] = new StatusDto(true, pair.Value);
        }

        string[] types = [..settings.ToggleActivities, ..settings.RecordActivities];
        var userId = profileService.GetInfo()?.Id;
        
        var counts = types.Select(x => 
            new ActivityCount(entityName, recordId, x)).ToArray();

        Dictionary<string, bool>? statusDict = null;
        if (userId is not null)
        {
            var activities = types.Select(x 
                => new Activity(entityName, recordId, x, userId)
            ).ToArray();
            statusDict = settings.EnableBuffering 
                ? await GetStatusDictFromBuffer(activities) 
                : await GetStatusDict(activities, ct);
        }

        var countDict = settings.EnableBuffering
            ? await GetCountDictFromBuffer(counts)
            : await GetCountDict(counts, ct);

        foreach (var t in types)
        {
            var isActive = statusDict is not null && statusDict.TryGetValue(t, out var b) && b;
            var count = countDict.TryGetValue(t, out var l) ? l : 0;
            ret[t] = new StatusDto(isActive, count);
        }

        return ret;
    }

    public async Task<Dictionary<string, long>> Record(
        string entityName,
        long recordId,
        string[] activityTypes,
        CancellationToken ct)
    {
        await EnsureEntityRecordExists(entityName, recordId,ct);
        
        if (activityTypes.Any(t => 
                !settings.RecordActivities.Contains(t) &&
                !settings.AutoRecordActivities.Contains(t)))
        {
            throw new ResultException("One or more activity types are not supported.");
        }

        var userId = profileService.GetInfo()?.Id;
        var activities = userId is not null
            ? activityTypes.Select(x => new Activity(entityName, recordId, x, userId)).ToArray()
            : [];

        var counts = activityTypes
            .Select(x => new ActivityCount(entityName, recordId, x,1))
            .ToArray();

        var result = new Dictionary<string, long>();

        if (settings.EnableBuffering)
        {
            await statusBuffer.Set( activities.Select(a => a.Key()).ToArray());
            foreach (var count in counts)
            {
                result[count.ActivityType] = await countBuffer.Increase(count.Key(), 1, GetCount);
            }
        }
        else
        {
            await UpsertActivities(activities,ct);
            foreach (var count in counts)
            {
                result[count.ActivityType] = await dao.Increase(
                    ActivityCounts.TableName, count.Condition(true),
                    ActivityCounts.CountField, 1, ct);
            }
        }
        return result;
    }

    public async Task<long> Toggle(
        string entityName,
        long recordId,
        string activityType,
        bool isActive,
        CancellationToken ct)
    {
        if (!settings.ToggleActivities.Contains(activityType))
            throw new ResultException($"Activity type {activityType} is not supported");

        if (profileService.GetInfo() is not { Id: var userId })
            throw new ResultException("User is not logged in");

        var entity = await EnsureEntityRecordExists(entityName, recordId, ct);

        var activity = new Activity(entityName, recordId, activityType, userId,isActive);
        var count = new ActivityCount(entityName, recordId, activityType);
        var delta = isActive ? 1 : -1;

        if (settings.EnableBuffering)
        {
            return await statusBuffer.Toggle(activity.Key(), isActive, GetStatus) switch
            {
                true => await countBuffer.Increase(count.Key(), delta, GetCount),
                false => (await countBuffer.Get([count.Key()], GetCount)).FirstOrDefault().Value
            };
        }

        //only update is Active field, to determine if you should increase count
        var changed = await dao.UpdateOnConflict(
            Models.Activities.TableName,
            activity.UpsertRecord(false), 
            Models.Activities.KeyFields, 
            ct);
        return changed switch
        {
            true => await UpdateActivityMetaAndIncrease(),
            false => (await dao.FetchValues<long>(
                    ActivityCounts.TableName,
                    count.Condition(true),
                    null, null,
                    ActivityCounts.CountField,
                    ct))
                .FirstOrDefault().Value
        };

        async Task<long> UpdateActivityMetaAndIncrease()
        {
            if (activity.IsActive)
            {
                var loadedActivities = await LoadActivityMetaData(entity, [activity], ct);
                if (loadedActivities.Length == 0) throw new ResultException("No activities loaded");

                await dao.UpdateOnConflict(Models.Activities.TableName,
                    loadedActivities[0].UpsertRecord(true), Models.Activities.KeyFields, ct);
            }

            return await dao.Increase(
                ActivityCounts.TableName,
                count.Condition(true),
                ActivityCounts.CountField,
                delta,
                ct);
        }
    }

    private async Task<Activity[]> LoadActivityMetaData(Entity entity, Activity[] activities, CancellationToken ct)
    {
        var ids = activities
            .Where(x=>x.IsActive)
            .Select(x => x.RecordId.ToString())
            .ToArray();
        if (ids.Length == 0) return activities;
        
        var strAgs = new StrArgs
        {
            [entity.BookmarkQueryParamName] = ids
        };
        var records = await queryService.ListWithAction(entity.BookmarkQuery, new Span(),new Pagination(),strAgs,ct);
        var dict = records.ToDictionary(x => x[entity.PrimaryKey].ToString());

        var list = new List<Activity>();
        foreach (var ac in activities)
        {
            if (!ac.IsActive) list.Add(ac);
            if (dict.TryGetValue(ac.RecordId.ToString(), out var record))
            {
                list.Add(ac.LoadMetaData(entity, record));
            } 
        }
        return list.ToArray();
    }

    private async Task UpsertActivities(Activity[] activities,CancellationToken ct)
    {
        var groups = activities.GroupBy(a => a.EntityName);
        var toUpdate = new List<Record>();
        var entities = await schemaService.AllEntities(ct);
        
        foreach (var group in groups)
        {
            var entity = entities.FirstOrDefault(x=>x.Name == group.Key);
            if (entity == null) throw new ResultException($"Entity {group.Key} not found");
            var loadedActivities = await LoadActivityMetaData(entity,activities, ct);
            toUpdate.AddRange(loadedActivities.Select(x=>x.UpsertRecord(true)));
        }
        await dao.BatchUpdateOnConflict(
            Models.Activities.TableName,
            toUpdate.ToArray(),
            Models.Activities.KeyFields,
            ct);
    }

    private async Task<Entity> EnsureEntityRecordExists(string entityName, long recordId, CancellationToken ct)
    {
        var entities = await schemaService.AllEntities(ct);
        var entity = entities.FirstOrDefault(x => x.Name == entityName);
        if (entity is null) throw new ResultException("Entity not found");

        var maxId = await maxRecordIdCache.GetOrSet(entityName,
            async _ => await dao.MaxId(entity.TableName, entity.PrimaryKey, ct), ct);

        if (recordId < 1 || recordId > maxId)
        {
            throw new ResultException("Record id is out of range");
        }
        return entity;
    }
    
    private async Task<Dictionary<string, bool>> GetStatusDictFromBuffer(Activity[] status)
    {
        var keys = status.Select(Models.Activities.Key).ToArray();
        var dict = await statusBuffer.Get(keys, GetStatus);
        var ret = new Dictionary<string, bool>();
        foreach (var (key, value) in dict)
        {
            var activity = Models.Activities.Parse(key);
            ret[activity.ActivityType] = value;
        }
        return ret;
    }
    
    private  async Task<Dictionary<string,bool>> GetStatusDict(Activity[] activities, CancellationToken ct)
    {
        var userId = profileService.GetInfo()?.Id;
        if (activities.Length == 0 || userId is null) return [];
        
        return await dao.FetchValues<bool>(
            Models.Activities.TableName, 
            activities.First().Condition(false), 
            Models.Activities.TypeField,
            activities.Select(x=>x.ActivityType), 
            Models.Activities.ActiveField,
            ct);
    }

    private async Task<Dictionary<string, long>> GetCountDictFromBuffer(ActivityCount[] counts)
    {
        var dict = await countBuffer.Get(counts.Select(ActivityCounts.Key).ToArray(), GetCount);
        var ret = new Dictionary<string, long>();
        foreach (var (key, value) in dict)
        {
            var ct = ActivityCounts.Parse(key);
            ret[ct.ActivityType] = value;
        }
        return ret;
    }
    
    private async Task<Dictionary<string, long>> GetCountDict(ActivityCount[] counts, CancellationToken ct)
    {
        if (counts.Length == 0 ) return [];

        return await dao.FetchValues<long>(
            ActivityCounts.TableName,
            counts.First().Condition(false),
            ActivityCounts.TypeField,
            counts.Select(x => x.ActivityType),
            ActivityCounts.CountField,
            ct);
    }

    private async Task<bool> GetStatus(string key)
    {
        var activity = Models.Activities.Parse(key);
        var res = await dao.FetchValues<bool>(
            Models.Activities.TableName, 
            activity.Condition(true), 
            null,null,Models.Activities.ActiveField);
        return res.Count > 0 && res.First().Value;
    }

    private async Task<long> GetCount(string key)
    {
        var count = ActivityCounts.Parse(key);
        var res = await dao.FetchValues<long>(
            ActivityCounts.TableName, 
            count.Condition(true), 
            null,null,
            ActivityCounts.CountField);
        return res.Count > 0 ? res.First().Value:0;
    }
}