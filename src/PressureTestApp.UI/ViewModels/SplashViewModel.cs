using CommunityToolkit.Mvvm.ComponentModel;

namespace PressureTestApp.UI.ViewModels;

public partial class SplashViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "正在初始化...";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _versionText = $"v{Core.Configs.AppConstants.Version} · UoS V20-AMD64 · .NET 8";

    [ObservableProperty]
    private bool _check1 = false; // 配置加载

    [ObservableProperty]
    private bool _check2 = false; // 数据库

    [ObservableProperty]
    private bool _check3 = false; // PLC

    [ObservableProperty]
    private bool _check4 = false; // 网络
}
