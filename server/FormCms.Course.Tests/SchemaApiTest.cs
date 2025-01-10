using FormCMS.Cms.Services;
using FormCMS.CoreKit.ApiClient;
using FormCMS.Utils.RelationDbDao;
using FormCMS.Core.Descriptors;
using FormCMS.Utils.ResultExt;
using IdGen;

using Attribute = FormCMS.Core.Descriptors.Attribute;

namespace FormCMS.Course.Tests;

public class SchemaApiTest
{
    private readonly SchemaApiClient _schema;
    private readonly AccountApiClient _account;

    private static string PostEntity() => "schema_api_test_post" + new IdGenerator(0).CreateId();
    private const string Name = "name";

    public SchemaApiTest()
    {
        Util.SetTestConnectionString();

        WebAppClient<Program> webAppClient = new();
        _schema = new SchemaApiClient(webAppClient.GetHttpClient());
        _account = new AccountApiClient(webAppClient.GetHttpClient());
    }

    [Fact]
    public async Task AnonymousGetAll() => Assert.True((await _schema.All(null)).IsFailed);

    [Fact]
    public async Task AnonymousSave() => Assert.True((await _schema.SaveEntityDefine(TestSchema())).IsFailed);

    [Fact]
    public async Task GetAll_NUllType()
    {
        (await _account.EnsureLogin()).Ok();
        var items = await _schema.All(null).Ok();
        var len = items.Length;
        var schema = TestSchema();
        await _schema.SaveEntityDefine(schema);
        items = (await _schema.All(null)).Ok();
        Assert.Equal(len + 1, items.Length);
    }

    [Fact]
    public async Task GetAll_EntityType()
    {
        (await _account.EnsureLogin()).Ok();
        var items = (await _schema.All(SchemaType.Menu)).Ok();
        Assert.Single(items);
    }

    [Fact]
    public async Task GetTopMenuBar() => Assert.NotNull((await _schema.GetTopMenuBar()).Ok().Settings.Menu);

    [Fact]
    public async Task SaveSchemaAndOneAndGetLoaded()
    {
        var schema = TestSchema();
        (await _account.EnsureLogin()).Ok();
        schema = (await _schema.SaveEntityDefine(schema)).Ok();
        (await _schema.GetLoadedEntity(schema.Name)).Ok();
        (await _schema.One(schema.Id)).Ok();
    }

    [Fact]
    public async Task SaveSchemaTwice()
    {
        var schema = TestSchema();
        (await _account.EnsureLogin()).Ok();
        var res = (await _schema.SaveEntityDefine(schema)).Ok();
        (await _schema.SaveEntityDefine(res)).Ok();
    }

    [Fact]
    public async Task SaveSchema_Update()
    {
        var schema = TestSchema();
        (await _account.EnsureLogin()).Ok();
        schema = (await _schema.SaveEntityDefine(schema)).Ok();
        schema = schema with { Settings = new Settings(Entity: schema.Settings.Entity! with { DefaultPageSize = 10 }) };
        (await _schema.SaveEntityDefine(schema)).Ok();
        var entity = (await _schema.GetLoadedEntity(schema.Name)).Ok();
        Assert.Equal(10, entity.DefaultPageSize);
    }

    [Fact]
    public async Task Delete_Success()
    {
        var schema = TestSchema();
        (await _account.EnsureLogin()).Ok();
        schema = (await _schema.SaveEntityDefine(schema)).Ok();
        (await _schema.Delete(schema.Id)).Ok();
        Assert.True((await _schema.GetLoadedEntity(schema.Name)).IsFailed);
    }

    [Fact]
    public async Task GetTableDefinitions_Success()
    {
        var schema = TestSchema();
        (await _schema.GetTableDefine(schema.Name)).Ok();
    }

    [Fact]
    public async Task GetGraphQlClientUrlOk()
    {
        (await _schema.GraphQlClientUrl()).Ok();
    }

    private static Schema TestSchema()
    {
        var name = PostEntity();
        return new Schema
        (
            Id: 0,
            Name: name,
            Type: SchemaType.Entity,
            Settings: new Settings
            {
                Entity = new Entity(Name: name,
                    PrimaryKey: "id",
                    TableName: name,
                    TitleAttribute: Name,
                    Attributes:
                    [
                        new Attribute
                        (
                            Field: Name,
                            DataType: DataType.String
                        )
                    ]
                )
            }
        );
    }
}