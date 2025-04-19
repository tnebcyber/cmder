using FormCMS.Core.Assets;
using FormCMS.Core.Descriptors;
using FormCMS.Infrastructure.RelationDbDao;
using FormCMS.Utils.DataModels;
using FormCMS.Utils.RecordExt;
using Humanizer;

namespace FormCMS.Activities.Models;

public record Activity(
    string EntityName,
    long RecordId,
    string ActivityType,
    string UserId,
    bool IsActive = true,
    long? Id = null,
    string Title ="", 
    string Url="", 
    string Image="", 
    string Subtitle="", 
    DateTime? PublishedAt=null
);

public record ActivityList(Activity[] Items, long TotalRecords);

public static class Activities
{
    public const string TableName = "__activities";

    public static readonly string ActiveField = nameof(Activity.IsActive).Camelize();
    public static readonly string TypeField = nameof(Activity.ActivityType).Camelize();
    
    public static readonly string[] KeyFields =
    [
        nameof(Activity.EntityName).Camelize(),
        nameof(Activity.RecordId).Camelize(),
        nameof(Activity.ActivityType).Camelize(),
        nameof(Activity.UserId).Camelize(),
    ];

    public static readonly Column[] Columns =
    [
        ColumnHelper.CreateCamelColumn<Activity>(x => x.Id!, ColumnType.Id),
        ColumnHelper.CreateCamelColumn<Activity, string>(x => x.EntityName),
        ColumnHelper.CreateCamelColumn<Activity, long>(x => x.RecordId),
        ColumnHelper.CreateCamelColumn<Activity, string>(x => x.ActivityType),
        ColumnHelper.CreateCamelColumn<Activity, string>(x => x.UserId),
        
        //use active, not deleted, the end point pass parameter ?type=like&active=true
        ColumnHelper.CreateCamelColumn<Activity, bool>(x => x.IsActive),

        ColumnHelper.CreateCamelColumn<Activity, string>(x => x.Title),
        ColumnHelper.CreateCamelColumn<Activity, string>(x => x.Url),
        ColumnHelper.CreateCamelColumn<Activity, string>(x => x.Subtitle),
        ColumnHelper.CreateCamelColumn<Activity, string>(x => x.Image),
        
        DefaultAttributeNames.PublishedAt.CreateCamelColumn(ColumnType.Datetime),
        
        
        DefaultColumnNames.CreatedAt.CreateCamelColumn(ColumnType.CreatedTime),
        DefaultColumnNames.UpdatedAt.CreateCamelColumn(ColumnType.UpdatedTime),
    ];

    public static Activity Parse(string recordKey)
    {
        var parts = recordKey.Split('.');
        return new Activity(parts[0], long.Parse(parts[1]), parts[2], parts[3]);
    }

    public static Activity LoadMetaData(this Activity activity, Entity entity, Record record)
    {

        activity = activity with { Url = entity.PageUrl + activity.RecordId };
        if (record.ByJsonPath<string>(entity.BookmarkTitleField, out var title))
        {
            activity = activity with { Title = Trim(title)};
        }

        if (record.ByJsonPath<Asset>(entity.BookmarkImageField, out var asset))
        {
            activity = activity with { Image = Trim(asset?.Url) };
        }

        if (record.ByJsonPath<string>(entity.BookmarkSubtitleField, out var subtitle))
        {
            activity = activity with { Subtitle = Trim(subtitle)};
        }

        if (record.ByJsonPath<DateTime>(entity.BookmarkPublishTimeField, out var publishTime))
        {
            activity = activity with { PublishedAt = publishTime };
        }

        return activity;
        
        string Trim(string? s) => s?.Length > 255 ? s[..255] : s??"";
    }

    public static string Key(this Activity activity)
        => $"{activity.EntityName}.{activity.RecordId}.{activity.ActivityType}.{activity.UserId}";
    
    public static Record UpsertRecord(this Activity activity, bool includeMetaData)
    {
        var whitList = new List<string>
        {
            nameof(Activity.EntityName),
            nameof(Activity.RecordId),
            nameof(Activity.ActivityType),
            nameof(Activity.UserId),
            nameof(Activity.IsActive),
        };
        if (includeMetaData)
        {
            whitList.AddRange([
                nameof(Activity.Title),
                nameof(Activity.Image),
                nameof(Activity.Subtitle),
                nameof(Activity.Url),
                nameof(Activity.PublishedAt)
            ]);
        }
        return RecordExtensions.FormObject(activity, [..whitList]);
    }
    
    public static Record Condition(this Activity activity,bool includeType)
    {
        var ret = new Dictionary<string, object>
        {
            {nameof(Activity.EntityName).Camelize(),activity.EntityName},
            {nameof(Activity.RecordId).Camelize(),activity.RecordId},
            {nameof(Activity.UserId).Camelize(),activity.UserId}
        };
        if (includeType)
        {
            ret[nameof(Activity.ActivityType).Camelize()] = activity.ActivityType;
        }
        return ret;
    }

    public static SqlKata.Query List(string userId, string activityType)
        => new SqlKata.Query(TableName)
            .Select(
                nameof(Activity.EntityName).Camelize(),
                nameof(Activity.RecordId).Camelize(),
                nameof(DefaultColumnNames.UpdatedAt).Camelize()
            )
            .Where(nameof(Activity.UserId).Camelize(), userId)
            .Where(nameof(Activity.ActivityType).Camelize(), activityType);
}