using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Services;
using PressureTestApp.Data.Repositories;
using PressureTestApp.UI.Views;
using System;
using System.Threading.Tasks;

namespace PressureTestApp.UI.ViewModels;

public partial class LoginViewModel : ObservableObject/// : INotifyPropertyChanged
{
    private readonly UserRepository _userRepo;
    private readonly AppConfig _appConfig;
    private readonly AuthenticationConfig _authConfig;
    private readonly ITaskApiService _taskApi;
    private readonly IFaceAuthService _faceAuth;

    [ObservableProperty] private string _userId = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _rememberPassword = true;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoading;

    /// <summary>登录方式：true=密码 false=人脸</summary>
    [ObservableProperty] private bool _isPasswordMode = true;

    /// <summary>人脸识别是否可用（当前永远 false，UI 中按钮置灰）</summary>
    public bool IsFaceAuthAvailable => _faceAuth.IsAvailable;

    /// <summary>登录成功后回调（由 View 订阅，跳转主窗口）</summary>
    public event Action? LoginSucceeded;

    /// <summary>由 LoginWindow 在 Loaded 时注入，用于弹出认证对话框。</summary>
    public Window? HostWindow { get; set; }

    [ObservableProperty] private string _deviceAuthHint = "设备认证 / 重新认证";

    /// <summary>与旧版 isAuthentication=是 时显示设备认证入口。</summary>
    [ObservableProperty] private bool _showDeviceAuthButton = true;

    public LoginViewModel()
    {
        _userRepo = App.Services.GetRequiredService<UserRepository>();
        _appConfig = App.Services.GetRequiredService<AppConfig>();
        _authConfig = App.Services.GetRequiredService<AuthenticationConfig>();
        ShowDeviceAuthButton = false; // 现场仅 MES，不用 EPE 设备认证入口
        _taskApi = App.Services.GetRequiredService<ITaskApiService>();
        _faceAuth = App.Services.GetRequiredService<IFaceAuthService>();

        RefreshDeviceAuthHint();

        if (_appConfig.RememberPassword)
        {
            UserId = _appConfig.LastUserId;
            Password = _appConfig.LastPassword;
        }
    }

    private void RefreshDeviceAuthHint()
    {
        var id = _taskApi.DeviceNumber ?? _authConfig.Authentication;
        DeviceAuthHint = string.IsNullOrWhiteSpace(id)
            ? "设备认证（未认证）"
            : $"设备已认证 · {id}";
    }

    [RelayCommand]
    private async Task OpenDeviceAuthAsync()
    {
        if (HostWindow == null)
            return;

        var vm = App.Services.GetRequiredService<DeviceAuthViewModel>();
        var win = new DeviceAuthWindow { DataContext = vm };
        await win.ShowDialog(HostWindow);
        RefreshDeviceAuthHint();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入工号和密码";
            return;
        }

        IsLoading = true;
        try
        {
            var user = await _userRepo.FindByJobNumberAsync(UserId.Trim());// 根据工号查找用户（仅启用的），不区分大小写
            if (user == null)
            {
                ErrorMessage = "用户不存在或已被禁用";
                return;
            }

            if (string.IsNullOrEmpty(user.PasswordHash)
                || !string.Equals(PasswordHelper.SHA512Encode(user.JobNumber, Password), user.PasswordHash, StringComparison.Ordinal))
            {
                ErrorMessage = "密码错误";
                return;
            }

            // 写入全局会话
            VarHelper.CurrentUserId = user.JobNumber;
            VarHelper.CurrentUserName = user.Name;
            VarHelper.CurrentUserRole = user.Role;

            // 记住密码
            _appConfig.RememberPassword = RememberPassword;
            _appConfig.LastUserId = RememberPassword ? UserId : "";
            _appConfig.LastPassword = RememberPassword ? Password : "";
            _appConfig.Save();

            await _userRepo.UpdateLastLoginAsync(user.Id);
            NlogHelper.Info($"用户登录成功：{user.JobNumber} · {user.Name}");

            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "登录异常");
            ErrorMessage = $"登录失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SwitchToPasswordMode() => IsPasswordMode = true;

    [RelayCommand]
    private void SwitchToFaceMode()
    {
        if (!IsFaceAuthAvailable)
        {
            ErrorMessage = "人脸识别功能暂未启用，待硬件接入后开放";
            return;
        }
        IsPasswordMode = false;
    }
}
