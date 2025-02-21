using System.Text.Json;
using System.Text.Json.Serialization;
using FormCMS.AuditLogging.Handlers;
using FormCMS.AuditLogging.Models;
using FormCMS.AuditLogging.Services;
using FormCMS.Core.HookFactory;
using FormCMS.Core.Identities;
using FormCMS.Utils.RecordExt;

namespace FormCMS.AuditLogging.Builders;

public sealed class AuditLogBuilder(ILogger<AuditLogBuilder> logger )
{
    public static IServiceCollection AddAuditLog(IServiceCollection services)
    {
        services.AddSingleton<AuditLogBuilder>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.ConfigureHttpJsonOptions(AddCamelEnumConverter<ActionType>);

        return services;
    }

    private static void AddCamelEnumConverter<T>(Microsoft.AspNetCore.Http.Json.JsonOptions options) where T : struct, Enum
        => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<T>(JsonNamingPolicy.CamelCase));
    public async Task<WebApplication> UseAuditLog(WebApplication app)
    {
        logger.LogInformation(
            """
            *********************************************************
            Using AuditLog
            *********************************************************
            """);

        app.Services.GetService<RestrictedFeatures>()?.Menus.Add(AuditLoggingConstants.MenuId);

        using var scope = app.Services.CreateScope();
        var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        await auditLogService.EnsureAuditLogTable();
        
        var options = app.Services.GetRequiredService<SystemSettings>();
        var apiGroup = app.MapGroup(options.RouteOptions.ApiBaseUrl);
        apiGroup.MapGroup("/audit_log").MapAuditLogHandlers();
        
        
        var registry = app.Services.GetRequiredService<HookRegistry>();
        registry.EntityPostAdd.RegisterDynamic("*",
            async (IAuditLogService service, EntityPostAddArgs args) =>
            {
                await service.AddLog(
                    ActionType.Create, 
                    args.Entity.Name, 
                    args.Record.GetStrOrEmpty(args.Entity.PrimaryKey),
                    args.Record.GetStrOrEmpty(args.Entity.LabelAttributeName),
                    args.Record) ;
                return args;
            }
        );
        registry.EntityPostDel.RegisterDynamic( "*",
            async (IAuditLogService service, EntityPostDelArgs args) =>
            {
                await service.AddLog(
                    ActionType.Delete, 
                    args.Entity.Name, 
                    args.Record.GetStrOrEmpty(args.Entity.PrimaryKey),
                    args.Record.GetStrOrEmpty(args.Entity.LabelAttributeName),
                    args.Record
                    ) ;
                return args;
            }
        );
        registry.EntityPostUpdate.RegisterDynamic("*",
            async (IAuditLogService service, EntityPostUpdateArgs args) =>
            {
                await service.AddLog(
                    ActionType.Update,
                    args.Entity.Name,
                    args.Record.GetStrOrEmpty(args.Entity.PrimaryKey),
                    args.Record.GetStrOrEmpty(args.Entity.LabelAttributeName),
                    args.Record
                );
                return args;
            }
        );
        return app;
    }

}