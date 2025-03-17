using FormCMS.Auth;
using FormCMS.Infrastructure.FileStore;
using FormCMS.Utils.ResultExt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace FormCMS.Course;

public class WebApp(
    WebApplicationBuilder builder,
    string databaseProvider, 
    string databaseConnectionString, 
    string? redisConnectionString, 
    AzureBlobStoreOptions? azureBlobStoreOptions
    )
{
    private const string Cors = "cors";

    public async Task<WebApplication> Build()
    {
        AddDabaseService();
        builder.Services.AddCmsAuth<IdentityUser, IdentityRole, CmsDbContext>();
        builder.Services.AddAuditLog();
        
        AddOutputCachePolicy();
        builder.AddServiceDefaults();
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddOpenApi();
            AddCorsPolicy();
        }

        TryAddHybridCache();
        TryAzureBlobStore();
        
        var app = builder.Build();
        app.MapDefaultEndpoints();
        app.UseOutputCache();
        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference();
            app.MapOpenApi();
            app.UseCors(Cors);
        }

        await EnsureDbCreatedAsync();
        await app.UseCmsAsync();
        await EnsureUserCreatedAsync();
        
        return app;

        async Task EnsureDbCreatedAsync()
        {
            using var scope = app.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            await ctx.Database.EnsureCreatedAsync();
        }

        async Task EnsureUserCreatedAsync()
        {
            await app.EnsureCmsUser("sadmin@cms.com", "Admin1!", [Roles.Sa]).Ok();
            await app.EnsureCmsUser("admin@cms.com", "Admin1!", [Roles.Admin]).Ok();
        }
    }

    private void AddDabaseService()
    {
        _ = databaseProvider switch
        {
            Constants.Sqlite => builder.Services.AddDbContext<CmsDbContext>(options => options.UseSqlite(databaseConnectionString))
                .AddSqliteCms(databaseConnectionString),
            Constants.Postgres => builder.Services.AddDbContext<CmsDbContext>(options => options.UseNpgsql(databaseConnectionString))
                .AddPostgresCms(databaseConnectionString),
            Constants.SqlServer => builder.Services.AddDbContext<CmsDbContext>(options => options.UseSqlServer(databaseConnectionString))
                .AddSqlServerCms(databaseConnectionString),
            _ => throw new Exception("Database provider not found")
        };
    }
    private void TryAzureBlobStore()
    {
        if (azureBlobStoreOptions is null) return;
        builder.Services.AddSingleton(azureBlobStoreOptions);
        builder.Services.AddSingleton<IFileStore, AzureBlobStore>();
    }

    private void TryAddHybridCache()
    {
        if (redisConnectionString is null) return;
        builder.AddRedisDistributedCache(connectionName: Constants.Redis);
        builder.Services.AddHybridCache();
    }

    private void AddOutputCachePolicy()
    {
        builder.Services.AddOutputCache(cacheOption =>
        {
            cacheOption.AddBasePolicy(policyBuilder => policyBuilder.Expire(TimeSpan.FromMinutes(1)));
            cacheOption.AddPolicy(SystemSettings.DefaultPageCachePolicyName,
                b => b.Expire(TimeSpan.FromMinutes(1)));
            cacheOption.AddPolicy(SystemSettings.DefaultQueryCachePolicyName,
                b => b.Expire(TimeSpan.FromSeconds(1)));
        });
    }

    private  void AddCorsPolicy()
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                Cors,
                policy =>
                {
                    policy.WithOrigins("http://127.0.0.1:5173")
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
        });
    }
}