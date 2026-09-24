using PressureTestApp.Core.Models;

namespace PressureTestApp.Core.Helpers;

/// <summary>旧库 Users.Permission（中文）与程序内 UserRole 互转</summary>
public static class UserPermissionMapper
{
    public static string RoleToPermission(UserRole role) => role switch
    {
        UserRole.Admin => "管理员",
        UserRole.Assistant => "工艺员",
        UserRole.Inspector => "互检员",
        UserRole.QualityChecker => "质检员",
        _ => "操作员"
    };

    public static UserRole PermissionToRole(string? permission) => permission switch
    {
        "管理员" => UserRole.Admin,
        "工艺员" => UserRole.Assistant,
        "互检员" => UserRole.Inspector,
        "质检员" => UserRole.QualityChecker,
        "操作员" => UserRole.Operator,
        _ => UserRole.Operator
    };
}
