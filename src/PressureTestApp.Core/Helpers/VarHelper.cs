using PressureTestApp.Core.Models;

namespace PressureTestApp.Core.Helpers;

/// <summary>
/// 全局变量容器（保留原项目接口）。
/// 注意：不要存放需要数据绑定的状态，这里只放纯静态全局值。
/// 数据绑定相关请用 ViewModel + ObservableProperty。
/// </summary>
public static class VarHelper
{
    /// <summary>当前登录用户工号</summary>
    public static string? CurrentUserId { get; set; }

    /// <summary>当前登录用户姓名</summary>
    public static string? CurrentUserName { get; set; }

    /// <summary>当前用户角色</summary>
    public static UserRole CurrentUserRole { get; set; } = UserRole.Operator;

    /// <summary>当前工作模式</summary>
    public static WorkMode CurrentWorkMode { get; set; } = WorkMode.Offline;

    /// <summary>网络是否可用</summary>
    public static bool NetworkState { get; set; }

    /// <summary>设备认证状态</summary>
    public static bool DeviceAuthorized { get; set; }

    /// <summary>设备唯一编号</summary>
    public static string? DeviceId { get; set; }

    /// <summary>当前选中的产品型号编号（离线模式用）</summary>
    public static string? CurrentProductCode { get; set; }

    /// <summary>当前任务编号</summary>
    public static string? CurrentTaskId { get; set; }

    /// <summary>清理用户登录态</summary>
    public static void ClearUserSession()
    {
        CurrentUserId = null;
        CurrentUserName = null;
        CurrentUserRole = UserRole.Operator;
    }
}
