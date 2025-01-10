using FormCMS.CoreKit.RelationDbQuery;

namespace FormCMS.Core.Descriptors;

public record Junction(
    LoadedEntity JunctionEntity,
    LoadedEntity TargetEntity,
    LoadedEntity SourceEntity,
    
    LoadedAttribute SourceAttribute,
    LoadedAttribute TargetAttribute
);

public static class JunctionHelper
{
    public static Junction Junction(LoadedEntity sourceEntity, LoadedEntity targetEntity,
        LoadedAttribute crossAttribute)
    {
        var tableName = GetJunctionTableName(sourceEntity.Name, targetEntity.Name);
        var id = new LoadedAttribute(tableName, DefaultFields.Id);
        var deleted = new LoadedAttribute(tableName, DefaultFields.Deleted);
        sourceEntity = sourceEntity with
        {
            Attributes =
            [..sourceEntity.Attributes.Select(x => x.Field == crossAttribute.Field ? x with { Junction = null } : x)]
        };

        var sourceAttribute = new LoadedAttribute
        (
            Field: $"{sourceEntity.Name}_id",
            TableName: tableName,
            DataType: DataType.Int,
            DisplayType:DisplayType.Number
        );

        var targetAttribute = new LoadedAttribute
        (
            Field: $"{targetEntity.Name}_id",
            TableName: tableName,
            DataType: DataType.Int,
            DisplayType:DisplayType.Number
        );
        
        var idAttr = new LoadedAttribute
        (
            Field: DefaultFields.Id,
            TableName: tableName,
            DataType: DataType.Int,
            DisplayType:DisplayType.Number
        );

        var created = new LoadedAttribute
        (
            Field: DefaultFields.CreatedAt,
            TableName: tableName,
            DataType: DataType.Datetime,
            DisplayType:DisplayType.Datetime
        );

        var updated = new LoadedAttribute
        (
            Field: DefaultFields.UpdatedAt,
            TableName: tableName,
            DataType: DataType.Datetime,
            DisplayType:DisplayType.Datetime
        );

        var junctionEntity = new LoadedEntity
        (
            Attributes: [idAttr,sourceAttribute, targetAttribute,created,updated],
            PrimaryKeyAttribute: id,
            LoadedTitleAttribute: id,
            DeletedAttribute: deleted,
            Name: tableName,
            TableName: tableName
        );
        return new Junction(
            JunctionEntity: junctionEntity,
            TargetEntity: targetEntity,
            SourceEntity: sourceEntity,
            SourceAttribute: sourceAttribute,
            TargetAttribute: targetAttribute
        );
    }

    private static string GetJunctionTableName(string sourceName, string targetName)
    {
        string[] arr = [sourceName, targetName];
        var sorted = arr.OrderBy(x => x).ToArray();

        return $"{sorted[0]}_{sorted[1]}";
    }

    public static SqlKata.Query Delete(this Junction c, ValidValue id, Record[] targetItems)
    {
        var vals = targetItems.Select(x => x[c.TargetEntity.PrimaryKey]);
        return new SqlKata.Query(c.JunctionEntity.TableName).Where(c.SourceAttribute.Field, id.ObjectValue)
            .WhereIn(c.TargetAttribute.Field, vals).AsUpdate([c.JunctionEntity.DeletedAttribute.Field], [true]);
    }

    public static SqlKata.Query Insert(this Junction c, ValidValue id, Record[] targetItems)
    {
        string[] cols = [c.SourceAttribute.Field, c.TargetAttribute.Field];
        var vals = targetItems.Select(x => { return new[] { id.ObjectValue, x[c.TargetEntity.PrimaryKey] }; });

        return new SqlKata.Query(c.JunctionEntity.TableName).AsInsert(cols, vals);
    }

    public static SqlKata.Query GetNotRelatedItems(
        this Junction c,
        IEnumerable<LoadedAttribute> selectAttributes,
        IEnumerable<ValidFilter> filters,
        IEnumerable<ValidSort> sorts,
        ValidPagination pagination,
        IEnumerable<ValidValue> sourceIds)
    {
        var baseQuery = c.TargetEntity.Basic().Select(selectAttributes.Select(x => x.AddTableModifier()));
        c.ApplyNotRelatedFilter(baseQuery, sourceIds);
        baseQuery.ApplyPagination(pagination);
        baseQuery.ApplyFilters(filters);
        baseQuery.ApplySorts(sorts);
        return baseQuery;
    }

    public static SqlKata.Query GetRelatedItems(
        this Junction c,
        IEnumerable<ValidFilter> filters,
        ValidSort[] sorts,
        ValidPagination? pagination,
        ValidSpan? span,
        IEnumerable<LoadedAttribute> selectAttributes,
        IEnumerable<ValidValue> sourceIds)
    {
        selectAttributes = [..selectAttributes, c.SourceAttribute];
        var query = c.TargetEntity.GetCommonListQuery(filters,sorts,pagination,span,selectAttributes);
        c.ApplyJunctionFilter(query, sourceIds);
        return query;
    }

    public static SqlKata.Query GetNotRelatedItemsCount(
        this Junction c,
        IEnumerable<ValidFilter> filters,
        IEnumerable<ValidValue> sourceIds)
    {
        var query = c.TargetEntity.Basic();
        c.ApplyNotRelatedFilter(query, sourceIds);
        query.ApplyFilters(filters);
        return query;
    }

    public static SqlKata.Query GetRelatedItemsCount(
        this Junction c,
        ValidFilter[] filters,
        IEnumerable<ValidValue> sourceIds)
    {
        var query = c.TargetEntity.GetCommonCountQuery(filters);
        c.ApplyJunctionFilter(query, sourceIds);
        return query;
    }

    private static void ApplyJunctionFilter(
        this Junction c,
        SqlKata.Query baseQuery,
        IEnumerable<ValidValue> sourceIds)
    {

        var (a, b) = (c.TargetEntity.PrimaryKeyAttribute.AddTableModifier(), c.TargetAttribute.AddTableModifier());
        baseQuery.Join(c.JunctionEntity.TableName, a, b)
            .WhereIn(c.SourceAttribute.AddTableModifier(), sourceIds.GetValues())
            .Where(c.JunctionEntity.DeletedAttribute.AddTableModifier(), false);
    }

    private static void ApplyNotRelatedFilter(
        this Junction c,
        SqlKata.Query baseQuery,
        IEnumerable<ValidValue> sourceIds)
    {
        var (a, b) = (c.TargetEntity.PrimaryKeyAttribute.AddTableModifier(), c.TargetAttribute.AddTableModifier());
        baseQuery.LeftJoin(c.JunctionEntity.TableName,
            j => j.On(a, b)
                .WhereIn(c.SourceAttribute.AddTableModifier(), sourceIds.GetValues())
                .Where(c.JunctionEntity.DeletedAttribute.AddTableModifier(), false)
        ).WhereNull(c.SourceAttribute.AddTableModifier());
    }
}