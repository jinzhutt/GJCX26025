using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PressureTestApp.Communication;
using PressureTestApp.Communication.Api;
using PressureTestApp.Communication.Excel;
using PressureTestApp.Communication.FaceAuth;
using PressureTestApp.Communication.Modbus;
using PressureTestApp.Communication.Plc;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Services;
using PressureTestApp.Data;
using PressureTestApp.Data.Repositories;
using PressureTestApp.UI.Services;
using PressureTestApp.UI.ViewModels;
using PressureTestApp.UI.Views;

using System;
using System.IO;

namespace PressureTestApp.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static IConfiguration Configuration { get; private set; } = null!; // 新增
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppConstants.EnsureDirectories();
        DatabaseConfig.EnsureUserIniFromBundled();
        AuthenticationConfig.EnsureUserIniFromBundled();
        ProductionSystemConfig.EnsureUserIniFromBundled();

        NlogHelper.Initialize(AppConstants.LogDir);
        NlogHelper.Info("==================== 应用启动 ====================");
        NlogHelper.Info($"版本：{AppConstants.Version} · {Environment.OSVersion}");

        // ====================== 加载配置文件 ======================
        var basePath = AppContext.BaseDirectory;
        var configPath = Path.Combine(basePath, "config", "appsettings.json");

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(configPath, optional: false, reloadOnChange: true);

        Configuration = configBuilder.Build();
        // ==========================================================

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        try
        {
            Services.GetRequiredService<DeviceStatusQueryHost>().Start();
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "MES 4.6 状态查询服务启动失败");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new SplashWindow
            {
                DataContext = Services.GetRequiredService<SplashViewModel>()
            };
            desktop.ShutdownRequested += (_, _) =>
            {
                try { Services.GetService<DeviceStatusQueryHost>()?.Stop(); }
                catch { /* ignore */ }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ===== 配置 =====
        services.AddSingleton<DatabaseConfig>(_ => new DatabaseConfig());
        services.AddSingleton<PlcConfig>(_ => new PlcConfig());
        services.AddSingleton<AuthenticationConfig>(_ => new AuthenticationConfig());
        services.AddSingleton<ProductionSystemConfig>(_ => new ProductionSystemConfig());
        services.AddSingleton<AppConfig>(_ => new AppConfig());
        services.AddSingleton<InstrumentsConfigFile>(_ => InstrumentsConfigFile.Load());

        // 绑定 appsettings.json 中的 Plc200Config
        services.Configure<Plc200Config>(Configuration.GetSection(nameof(Plc200Config)));
        
        // 绑定 appsettings.json 中的 TempHumiConfig
        services.Configure<TempHumiConfig>(Configuration.GetSection(nameof(TempHumiConfig)));
        
        // 绑定 appsettings.json 中的 InsulationConfig
        services.Configure<InsulationConfig>(Configuration.GetSection(nameof(InsulationConfig)));
        
        // 绑定 appsettings.json 中的 TurnConfig
        services.Configure<TurnConfig>(Configuration.GetSection(nameof(TurnConfig)));

        //温湿度传感器服务
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<TempHumiConfig>>().Value;
            if (!config.Enabled)
                return new TemphumiHelper();

            var helper = new TemphumiHelper();
            try
            {
                helper.Init(config.PortName);
                NlogHelper.Info($"温湿度传感器初始化成功：{config.PortName}");
            }
            catch (Exception ex)
            {
                NlogHelper.Warn(ex, $"温湿度传感器初始化失败：{ex.Message}");
            }
            return helper;
        });

        // ===== 数据 =====
        services.AddSingleton<KingbaseContext>();
        services.AddTransient<UserRepository>();
        services.AddTransient<ProductRepository>();
        services.AddTransient<VehicleTypeRepository>();
        services.AddTransient<HoldTaskRepository>();
        services.AddTransient<LegacyTaskRepository>();
        services.AddTransient<OfflineRecordRepository>();
        services.AddTransient<MesPendingUploadRepository>();
        services.AddTransient<ProjectTrainRepository>();
        services.AddTransient<DeviceCounterRepository>();
        services.AddTransient<InspectionStaffRepository>();

        // ===== 通信 =====
        services.AddSingleton<IXdpPlcCommunicator>(sp =>
        {
            var plcConfig = sp.GetRequiredService<IOptions<Plc200Config>>().Value;

            if (!plcConfig.Enabled)
                return new MockXdpPlcCommunicator();

            try
            {
                var smart200 = new XdpPlcCommunicator(plcConfig.IpAddress);

                if (smart200.IsConnected)
                {
                    if (!string.IsNullOrEmpty(plcConfig.CsvFilePath))
                        smart200.MappingPlcFromCSV(plcConfig.CsvFilePath);
                    NlogHelper.Info($"PLC 连接成功：{plcConfig.IpAddress}");
                    return smart200;
                }
                else
                {
                    NlogHelper.Warn($"PLC 连接失败（IP: {plcConfig.IpAddress}），自动切换到模拟模式");
                    return new MockXdpPlcCommunicator();
                }
            }
            catch (Exception ex)
            {
                NlogHelper.Warn(ex, $"PLC 连接异常（IP: {plcConfig.IpAddress}），自动切换到模拟模式");
                return new MockXdpPlcCommunicator();
            }
        });

        services.AddSingleton<ISerialInstrumentService, SerialModbusService>();
        services.AddSingleton<IFaceAuthService, NoOpFaceAuthService>();
        services.AddSingleton<IExcelImportService, ExcelImportService>();
        services.AddSingleton<IReportService, ReportService>();

        // ===== MES（现场仅对接 MES，不再注册 EPE）=====
        services.AddSingleton<MesApiClient>();
        services.AddSingleton<MesPendingUploadService>();
        services.AddSingleton<DeviceWorkReadyState>();
        services.AddSingleton<DeviceStatusQueryHost>();
        services.AddTransient<TaskDownloadService>();
        services.AddSingleton<ITaskApiService, MesOnlyTaskApiService>();

        // ===== ViewModel =====
        services.AddTransient<SplashViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DeviceAuthViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<TaskUploadViewModel>();
        services.AddTransient<TaskViewViewModel>();
        services.AddTransient<TaskDownloadSelectViewModel>();
    }
}
