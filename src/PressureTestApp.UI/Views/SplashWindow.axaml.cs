using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Data;
using PressureTestApp.UI.ViewModels;
using System;
using System.Threading.Tasks;

namespace PressureTestApp.UI.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await StartupChecksAsync();
    }

    /// <summary>
    /// 启动检查流程：配置 → 数据库 → 网络 → 跳转登录
    /// </summary>
    private async Task StartupChecksAsync()
    {
        if (DataContext is not SplashViewModel vm) return;

        try
        {
            // 1. 加载配置
            await StepAsync(vm, "正在加载配置...", 15, async () =>
            {
                DatabaseConfig.EnsureUserIniFromBundled();
                _ = App.Services.GetRequiredService<DatabaseConfig>();
                _ = App.Services.GetRequiredService<PlcConfig>();
                _ = App.Services.GetRequiredService<AuthenticationConfig>();
                _ = App.Services.GetRequiredService<ProductionSystemConfig>();
                _ = App.Services.GetRequiredService<AppConfig>();
                await Task.Delay(200);
            });

            // 2. 初始化数据库
            await StepAsync(vm, "正在连接 SQLite 数据库...", 50, async () =>
            {
                try
                {
                    var cfg = App.Services.GetRequiredService<DatabaseConfig>();
                    KingbaseContext.Initialize(cfg);
                }
                catch (Exception ex)
                {
                    NlogHelper.Error(ex, "SQLite 连接失败，应用以离线模式继续");
                }
                await Task.Delay(300);
            });

            // 3. 检测网络（暂时跳过实际检测，后面阶段补）
            await StepAsync(vm, "正在检测网络...", 75, async () =>
            {
                await Task.Delay(200);
            });

            // 4. 完成
            await StepAsync(vm, "启动完成", 100, async () =>
            {
                await Task.Delay(200);
            });

            // 5. 跳转到登录窗口
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var login = new LoginWindow
                {
                    DataContext = App.Services.GetRequiredService<LoginViewModel>()
                };
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = login;
                    login.Show();
                }
                Close();
            });
        }
        catch (Exception ex)
        {
            NlogHelper.Fatal(ex, "启动失败");
            vm.StatusText = $"启动失败：{ex.Message}";
        }
    }

    private async Task StepAsync(SplashViewModel vm, string text, double progress, Func<Task> action)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            vm.StatusText = text;
            vm.ProgressValue = progress;
        });
        await action();
    }
}
