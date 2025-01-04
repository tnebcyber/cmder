using FluentResults;

namespace FluentCMS.Auth.WebAppBuilders;

public interface IAuthBuilder
{
    WebApplication UseCmsAuth(WebApplication app);
    Task<Result> EnsureCmsUser(WebApplication app, string email, string password, string[] role);
}