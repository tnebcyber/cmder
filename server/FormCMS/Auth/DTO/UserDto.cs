namespace FormCMS.Auth.DTO;

public sealed record UserDto(
    string Id ,
    string Email ,
    string[] Roles ,
    string[] ReadWriteEntities ,
    string[] RestrictedReadWriteEntities ,
    string[] ReadonlyEntities ,
    string[] RestrictedReadonlyEntities ,
    string[] AllowedMenus 
);


public static class UserConstants
{
    public const string MenuSchemaBuilder = "menu_schema_builder";
    public const string MenuUsers = "menu_users";
    public const string MenuRoles = "menu_roles";
}