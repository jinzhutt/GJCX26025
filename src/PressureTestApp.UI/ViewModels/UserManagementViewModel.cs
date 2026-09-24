using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;
using PressureTestApp.Data.Entities;
using PressureTestApp.Data.Repositories;
using PressureTestApp.UI.Views;
using PressureTestApp.Communication.Plc;

namespace PressureTestApp.UI.ViewModels;

/// <summary>用户管理页左侧「数据设置」导航项（对应工业软件侧栏：用户 / 型号等）。</summary>
public enum UserManagementSidebarSection
{
    Users,
    Model,
    Parameter,
    Inspectors,
    Protection,
    Communication
}

public partial class UserManagementViewModel : ObservableObject
{
    private readonly UserRepository _userRepo;
    private readonly VehicleTypeRepository _vehicleTypeRepo;
    private readonly InspectionStaffRepository _inspectorStaffRepo;
    private readonly IXdpPlcCommunicator? _plc;//IPlc200Communicator
    private Dictionary<int, string> _typeIdCategoryMap = new();

    /// <summary>由用户管理页在 Loaded 时注入，用于 ShowDialog（避免 ApplicationLifetime.MainWindow 为空）</summary>
    public Window? HostWindow { get; set; }

    /// <summary>写入主界面「试验信息过程」日志（由 MainViewModel 注入）。</summary>
    public Action<string>? AppendMainTestProcessLog { get; set; }

    /// <summary>将 Para.ini同步到主界面「参数设定」（由 MainViewModel 注入）。</summary>
    public Action<ParaIniParameters>? SyncMainLoopPresets { get; set; }

    public Action? RefreshMainParameterSections { get; set; }

    private void LogMain(string msg) => AppendMainTestProcessLog?.Invoke(msg);

    private void PushParaToMainUi(ParaIniParameters para) => SyncMainLoopPresets?.Invoke(para);

    private void RefreshMainSections() => RefreshMainParameterSections?.Invoke();

    /// <summary>供视图在输入框失焦且内容相对获得焦点时已变化时调用。</summary>
    public void NotifyParameterFieldEdited(string displayName) =>
        LogMain($"修改参数「{displayName}」");

    private bool _parameterComboSuppress;

    private ProtectionParametersStore? _protectionSnapshotForLog;

    private string _snapModelCategory = "";
    private string _snapModelName = "";
    private string _snapModelRemark = "";

    public ObservableCollection<UserListItemViewModel> Users { get; } = new();

    /// <summary>左侧数据设置子导航当前项。</summary>
    [ObservableProperty] private UserManagementSidebarSection _sidebarSection = UserManagementSidebarSection.Users;

    public bool SidebarSelUsers => SidebarSection == UserManagementSidebarSection.Users;
    public bool SidebarSelModel => SidebarSection == UserManagementSidebarSection.Model;
    public bool SidebarSelParameter => SidebarSection == UserManagementSidebarSection.Parameter;
    public bool SidebarSelInspectors => SidebarSection == UserManagementSidebarSection.Inspectors;
    public bool SidebarSelProtection => SidebarSection == UserManagementSidebarSection.Protection;
    public bool SidebarSelCommunication => SidebarSection == UserManagementSidebarSection.Communication;

    public bool ShowUserListPanel => SidebarSection == UserManagementSidebarSection.Users;

    public bool ShowModelManagementPanel => SidebarSection == UserManagementSidebarSection.Model;

    public bool ShowParameterManagementPanel => SidebarSection == UserManagementSidebarSection.Parameter;

    public bool ShowInspectorsPanel => SidebarSection == UserManagementSidebarSection.Inspectors;

    public bool ShowProtectionParametersPanel => SidebarSection == UserManagementSidebarSection.Protection;

    public bool ShowCommunicationPanel => SidebarSection == UserManagementSidebarSection.Communication;

    /// <summary>顶部橙色大标题：参数页在内容区内自标题，此处不重复显示。</summary>
    public bool ShowSidebarOuterTitle =>
        SidebarSection != UserManagementSidebarSection.Users
        && SidebarSection != UserManagementSidebarSection.Parameter;

    public bool ShowSidebarPlaceholder => SidebarSection != UserManagementSidebarSection.Users
                                          && SidebarSection != UserManagementSidebarSection.Model
                                          && SidebarSection != UserManagementSidebarSection.Parameter
                                          && SidebarSection != UserManagementSidebarSection.Inspectors
                                          && SidebarSection != UserManagementSidebarSection.Protection
                                          && SidebarSection != UserManagementSidebarSection.Communication;

    public string SidebarPlaceholderTitle => SidebarSection switch
    {
        UserManagementSidebarSection.Model => "型号管理",
        UserManagementSidebarSection.Parameter => "参数管理",
    //    UserManagementSidebarSection.Inspectors => "检查人员",
    //    UserManagementSidebarSection.Protection => "保护参数",
        UserManagementSidebarSection.Communication => "通讯管理",
        _ => ""
    };

    [ObservableProperty] private UserListItemViewModel? _selectedUser;

    public ObservableCollection<ProductModelRowViewModel> ProductModels { get; } = new();

    [ObservableProperty] private ProductModelRowViewModel? _selectedProductModel;

    [ObservableProperty] private string _productTypeCategory = "耐压测试";

    [ObservableProperty] private string _editProductModelName = "";

    [ObservableProperty] private string _editProductRemark = "";

    [ObservableProperty] private string _paramDurationText = "";

    [ObservableProperty] private string _paramTargetPressureText = "";
    [ObservableProperty] private string _paramDisplacementOffsetText = "";
    [ObservableProperty] private string _paramDecelerationThresholdText = "";
    [ObservableProperty] private string _paramHighPressureFloatLimitText = "";
    [ObservableProperty] private string _paramReverseTimeText = "";
    /// <summary>0=慢速，1=快速（M421）</summary>
    [ObservableProperty] private int _paramFastSlowSelectedIndex;

    /// <summary>Para.ini 节名（可输入新名称，保存后新建一节）</summary>
    [ObservableProperty] private string _parameterName = ParaIniFile.RunSectionName;

    /// <summary>参数管理下拉：Para.ini 工艺参数节，不读车型库</summary>
    public ObservableCollection<string> ParameterNames { get; } = new();

    /// <summary>从 Para.ini 加载时的节名，保存时写回该节</summary>
    private string? _parameterEditingIniSection;

    public ObservableCollection<InspectorListItemViewModel> Inspectors { get; } = new();

    [ObservableProperty] private InspectorListItemViewModel? _selectedInspector;

    [ObservableProperty] private string _editAssistJob1 = "";

    [ObservableProperty] private string _editAssistName1 = "";

    [ObservableProperty] private string _editAssistJob2 = "";

    [ObservableProperty] private string _editAssistName2 = "";

    [ObservableProperty] private string _editAssistJob3 = "";

    [ObservableProperty] private string _editAssistName3 = "";

    [ObservableProperty] private string _editAssistJob4 = "";

    [ObservableProperty] private string _editAssistName4 = "";

    [ObservableProperty] private string _editMutualJob = "";

    [ObservableProperty] private string _editMutualName = "";

    [ObservableProperty] private string _editQualityJob = "";

    [ObservableProperty] private string _editQualityName = "";

    private bool _suppressInspectorEditPush;

    /// <summary>检查人员：选中行时的字段快照，供「修改」与日志对比（底部录入实时写回当前行，与表格同步）。</summary>
    private string _inspSnapJob1 = "", _inspSnapName1 = "", _inspSnapJob2 = "", _inspSnapName2 = "";
    private string _inspSnapJob3 = "", _inspSnapName3 = "", _inspSnapJob4 = "", _inspSnapName4 = "";
    private string _inspSnapMutualJob = "", _inspSnapMutualName = "", _inspSnapQualityJob = "", _inspSnapQualityName = "";

    // —— 保护参数（计数器阈值，单位：次）；界面值由 DeviceInspectConfig.ini 加载，不在此写死 ——
    [ObservableProperty] private string _protEmergencyStop = "";
    [ObservableProperty] private string _protGroundAlarm = "";
    [ObservableProperty] private string _protBell = "";
    [ObservableProperty] private string _protDoorInterlock = "";
    [ObservableProperty] private string _protVoltageRegLow = "";
    [ObservableProperty] private string _protVoltageRegHigh = "";
    [ObservableProperty] private string _protVoltageRegMotorAlarm = "";
    [ObservableProperty] private string _protMainContactor = "";
    [ObservableProperty] private string _protSlaveContactor = "";
    [ObservableProperty] private string _protDischarge = "";
    [ObservableProperty] private string _protElectricDoorLock = "";
    [ObservableProperty] private string _protStartButton = "";
    [ObservableProperty] private string _protStepUpButton = "";
    [ObservableProperty] private string _protStepDownButton = "";
    [ObservableProperty] private string _protStopButton = "";
    [ObservableProperty] private string _protResetButton = "";
    [ObservableProperty] private string _protTimerButton = "";
    [ObservableProperty] private string _protCounter = "";

    // —— 通讯管理参数 ——
    [ObservableProperty] private bool _commPlcEnabled = true;
    [ObservableProperty] private string _commPlcIp = "192.168.0.3";
    [ObservableProperty] private string _commPlcCsvFilePath = "ALL_PLC_XDP.csv";
    [ObservableProperty] private string _commPlcPort = "502";
    [ObservableProperty] private string _commTempHumiPort = "COM1";
    [ObservableProperty] private string _commTempHumiBaudRate = "9600";
    [ObservableProperty] private string _commInsulationPort = "COM2";
    [ObservableProperty] private string _commInsulationBaudRate = "9600";
    [ObservableProperty] private string _commTurnPort = "COM3";
    [ObservableProperty] private string _commTurnBaudRate = "9600";

    public ObservableCollection<string> ComPorts { get; } = new();

    private void LoadComPorts()
    {
        try
        {
            ComPorts.Clear();

            var ports = System.IO.Ports.SerialPort.GetPortNames();

            foreach (var port in ports)
            {
                ComPorts.Add(port);
            }
         }
        catch (Exception ex)
        {
            NlogHelper.Error($"加载串口列表失败: {ex.Message}");
        }
    }

    private void LoadParameterNames()
    {
        try
        {
            ParaIniFile.RemoveLegacyProcessSections();
            ParameterNames.Clear();
            foreach (var section in ParaIniFile.GetProcessParameterSectionNames())
                ParameterNames.Add(section);
            if (string.IsNullOrWhiteSpace(ParameterName) ||
                !ParameterNames.Any(x => string.Equals(x, ParameterName, StringComparison.OrdinalIgnoreCase)))
                ParameterName = ParaIniFile.RunSectionName;
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"加载参数名称列表失败: {ex.Message}");
            ParameterNames.Clear();
            ParameterNames.Add(ParaIniFile.RunSectionName);
            ParameterName = ParaIniFile.RunSectionName;
        }
    }

    partial void OnSelectedUserChanged(UserListItemViewModel? value)
    {
        ChangeUserCommand.NotifyCanExecuteChanged();
        DeleteUserCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProductModelChanged(ProductModelRowViewModel? value)
    {
        DeleteModelCommand.NotifyCanExecuteChanged();
        ChangeModelCommand.NotifyCanExecuteChanged();
        if (value == null)
        {
            EditProductModelName = "";
            EditProductRemark = "";
            _snapModelCategory = "";
            _snapModelName = "";
            _snapModelRemark = "";
        }
        else
        {
            EditProductModelName = value.ModelName;
            EditProductRemark = value.Remark;
            if (_typeIdCategoryMap.TryGetValue(value.TypeId, out var cat) && !string.IsNullOrWhiteSpace(cat))
                ProductTypeCategory = cat;
        }

        _snapModelCategory = (ProductTypeCategory ?? "").Trim();
        _snapModelName = (value?.ModelName ?? "").Trim();
        _snapModelRemark = (value?.Remark ?? "").Trim();
    }

    partial void OnSelectedInspectorChanged(InspectorListItemViewModel? value)
    {
        DeleteInspectorCommand.NotifyCanExecuteChanged();
        ChangeInspectorCommand.NotifyCanExecuteChanged();
        if (value == null)
        {
            ClearInspectorEditFields();
            CaptureInspectorEditBaseline(null);
        }
        else
        {
            LoadInspectorEditFromRow(value);
            CaptureInspectorEditBaseline(value);
        }
    }

    partial void OnEditAssistJob1Changed(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditAssistName1Changed(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditAssistJob2Changed(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditAssistName2Changed(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditAssistJob3Changed(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditAssistName3Changed(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditAssistJob4Changed(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditAssistName4Changed(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditMutualJobChanged(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditMutualNameChanged(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditQualityJobChanged(string value) => PushInspectorEditsToSelectedRow();

    partial void OnEditQualityNameChanged(string value) => PushInspectorEditsToSelectedRow();

    partial void OnSidebarSectionChanged(UserManagementSidebarSection value)
    {
        OnPropertyChanged(nameof(SidebarSelUsers));
        OnPropertyChanged(nameof(SidebarSelModel));
        OnPropertyChanged(nameof(SidebarSelParameter));
        OnPropertyChanged(nameof(SidebarSelInspectors));
        OnPropertyChanged(nameof(SidebarSelProtection));
        OnPropertyChanged(nameof(SidebarSelCommunication));
        OnPropertyChanged(nameof(ShowUserListPanel));
        OnPropertyChanged(nameof(ShowModelManagementPanel));
        OnPropertyChanged(nameof(ShowParameterManagementPanel));
        OnPropertyChanged(nameof(ShowInspectorsPanel));
        OnPropertyChanged(nameof(ShowProtectionParametersPanel));
        OnPropertyChanged(nameof(ShowCommunicationPanel));
        OnPropertyChanged(nameof(ShowSidebarOuterTitle));
        OnPropertyChanged(nameof(ShowSidebarPlaceholder));
        OnPropertyChanged(nameof(SidebarPlaceholderTitle));
        if (value == UserManagementSidebarSection.Model)
            _ = RefreshModelsAsync();
        if (value == UserManagementSidebarSection.Parameter)
        {
            LoadParameterNames();
            if (string.IsNullOrWhiteSpace(ParameterName))
                ParameterName = ParaIniFile.RunSectionName;
            LoadParameter();
        }
        if (value == UserManagementSidebarSection.Inspectors)
            _ = RefreshInspectorsAsync();
        if (value == UserManagementSidebarSection.Protection)
            _ = LoadProtectionParametersAsync();
        if (value == UserManagementSidebarSection.Communication)
            _ = LoadCommunicationParametersAsync();
    }

    [RelayCommand]
    private void SelectSidebarSection(string? sectionId)
    {
        var nextSection = sectionId switch
        {
            "users" => UserManagementSidebarSection.Users,
            "model" => UserManagementSidebarSection.Model,
            "parameter" => UserManagementSidebarSection.Parameter,
            "inspectors" => UserManagementSidebarSection.Inspectors,
            "protection" => UserManagementSidebarSection.Protection,
            "communication" => UserManagementSidebarSection.Communication,
            _ => UserManagementSidebarSection.Users
        };
        if (SidebarSection != nextSection)
        {
            LogMain(nextSection switch
            {
                UserManagementSidebarSection.Users => "使用「用户管理·用户管理」",
                UserManagementSidebarSection.Model => "使用「用户管理·型号管理」",
                UserManagementSidebarSection.Parameter => "使用「用户管理·参数管理」",
                UserManagementSidebarSection.Inspectors => "使用「用户管理·检查人员」",
                UserManagementSidebarSection.Protection => "使用「用户管理·保护参数」",
                UserManagementSidebarSection.Communication => "使用「用户管理·通讯管理」",
                _ => "使用「用户管理」"
            });
        }

        SidebarSection = nextSection;
    }

    public Action? NavigateBackToMainTest { get; set; }

    public UserManagementViewModel()
    {
        _userRepo = App.Services.GetRequiredService<UserRepository>();
        _vehicleTypeRepo = App.Services.GetRequiredService<VehicleTypeRepository>();
        _inspectorStaffRepo = App.Services.GetRequiredService<InspectionStaffRepository>();
        _plc = App.Services.GetService<IXdpPlcCommunicator>();//IPlc200Communicator

        LoadComPorts();
        LoadParameterNames();
    }

    public static string RoleDisplay(UserRole role) => role switch
    {
        UserRole.Admin => "管理员",
        UserRole.Assistant => "工艺员",
        UserRole.Operator => "操作员",
        UserRole.Inspector => "互检员",
        UserRole.QualityChecker => "质检员",
        _ => "操作员"
    };

    [RelayCommand]
    private void Close()
    {
        LogMain("使用「用户管理·返回试验主界面」");
        NavigateBackToMainTest?.Invoke();
    }

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        try
        {
            var list = await _vehicleTypeRepo.GetAllAsync();
            _typeIdCategoryMap = await _vehicleTypeRepo.GetCategoryNameByTypeIdAsync();
            // 型号管理：只读 Type 表车型；与 Para.ini 参数集无关
            NlogHelper.Info($"型号管理·从数据库加载车型 {list.Count} 条");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProductModels.Clear();
                foreach (var t in list)
                    ProductModels.Add(ProductModelRowViewModel.FromEntity(t));
                AppendDemoProductRowsIfEmpty();
                SelectedProductModel = ProductModels.FirstOrDefault();
                if (SelectedProductModel != null &&
                    _typeIdCategoryMap.TryGetValue(SelectedProductModel.TypeId, out var cat) &&
                    !string.IsNullOrWhiteSpace(cat))
                    ProductTypeCategory = cat;
            });
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"型号列表加载失败：{ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProductModels.Clear();
                AppendDemoProductRowsIfEmpty();
                SelectedProductModel = ProductModels.FirstOrDefault();
            });
        }
    }

    /// <summary>数据库无车型时的本地占位（车型1/2/3）；有 Type 表数据时不会用到。</summary>
    private void AppendDemoProductRowsIfEmpty()
    {
        if (ProductModels.Count > 0) return;
        ProductModels.Add(ProductModelRowViewModel.LocalDemo(-1, "车型1", ""));
        ProductModels.Add(ProductModelRowViewModel.LocalDemo(-2, "车型2", ""));
        ProductModels.Add(ProductModelRowViewModel.LocalDemo(-3, "车型3", ""));
    }

    [RelayCommand]
    private async Task AddModelAsync()
    {
        LogMain("使用「用户管理·型号管理·新增」");
        var owner = ResolveDialogOwner();
        var name = (EditProductModelName ?? "").Trim();
        if (string.IsNullOrEmpty(name))
            name = "新车型";
        var remark = (EditProductRemark ?? "").Trim();

        try
        {
            var typeId = await _vehicleTypeRepo.ResolveTypeIdAsync(ProductTypeCategory);
            var entity = new VehicleTypeEntity
            {
                Name = name,
                Mark = remark,
                TypeId = typeId
            };
            var newId = await _vehicleTypeRepo.InsertAsync(entity);
            NlogHelper.Info($"型号管理·已新增 Type 表：Id={newId} {entity.Name}");

            try
            {
                await RefreshModelsAsync();
            }
            catch (Exception refreshEx)
            {
                NlogHelper.Info($"型号列表刷新失败（数据已写入数据库）：{refreshEx.Message}");
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SelectedProductModel = ProductModels.FirstOrDefault(m => m.Id == newId)
                                       ?? ProductModels.LastOrDefault();
                if (SelectedProductModel != null)
                {
                    EditProductModelName = SelectedProductModel.ModelName;
                    EditProductRemark = SelectedProductModel.Remark;
                }
                else
                {
                    var row = ProductModelRowViewModel.FromEntity(entity);
                    ProductModels.Add(row);
                    SelectedProductModel = row;
                    EditProductModelName = row.ModelName;
                    EditProductRemark = row.Remark;
                }
            });
            if (owner != null)
                await new InfoNoticeWindow("新增成功！").ShowDialog(owner);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"型号新增失败：{ex.Message}");
            if (owner != null)
                await new InfoNoticeWindow($"新增失败：{ex.Message}\r请检查 database.ini 中的数据库路径，并确认数据库文件未被其他程序占用。")
                    .ShowDialog(owner);
        }
    }

    private bool CanModifyProductSelection() => SelectedProductModel != null;

    [RelayCommand(CanExecute = nameof(CanModifyProductSelection))]
    private async Task DeleteModelAsync()
    {
        if (SelectedProductModel == null) return;
        var owner = ResolveDialogOwner();
        if (owner == null)
        {
            NlogHelper.Info("型号管理·删除：未找到父窗口");
            return;
        }

        var row = SelectedProductModel;
        var deletedName = row.ModelName;
        var confirmed = await ConfirmDialogWindow.ShowConfirmAsync(owner,
            $"确定要删除型号「{row.ModelName}」吗？",
            "确认删除");
        if (!confirmed) return;

        LogMain("使用「用户管理·型号管理·删除」");

        if (row.IsLocalDemo)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProductModels.Remove(row);
                SelectedProductModel = ProductModels.FirstOrDefault();
            });
        }
        else
        {
            try
            {
                await _vehicleTypeRepo.DeleteAsync(row.Id);
                NlogHelper.Info($"型号管理·已删除 Type 表：Id={row.Id} {row.ModelName}");
                await RefreshModelsAsync();
            }
            catch (Exception ex)
            {
                NlogHelper.Info($"型号删除失败：{ex.Message}");
                return;
            }
        }

        if (owner != null)
            await new InfoNoticeWindow("删除成功").ShowDialog(owner);
    }

    [RelayCommand(CanExecute = nameof(CanModifyProductSelection))]
    private async Task ChangeModelAsync()
    {
        if (SelectedProductModel == null) return;
        var owner = ResolveDialogOwner();

        var name = (EditProductModelName ?? "").Trim();
        var remark = (EditProductRemark ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            NlogHelper.Info("型号管理·更改：产品型号不能为空");
            return;
        }

        var row = SelectedProductModel;
        var oldModelName = row.ModelName;
        var cat = (ProductTypeCategory ?? "").Trim();
        var changedModel = new List<string>();
        if (cat != _snapModelCategory) changedModel.Add("产品类型");
        if (name != _snapModelName) changedModel.Add("产品型号");
        if (remark != _snapModelRemark) changedModel.Add("备注");

        if (row.IsLocalDemo)
        {
            row.ModelName = name;
            row.Remark = remark;
            NlogHelper.Info($"型号管理·已更新（本地）：{name}");
        }
        else
        {
            try
            {
                var entity = await _vehicleTypeRepo.GetByIdAsync(row.Id);
                if (entity == null)
                {
                    NlogHelper.Info("型号管理·更改：记录不存在");
                    return;
                }

                entity.Name = name;
                entity.Mark = remark;
                entity.TypeId = await _vehicleTypeRepo.ResolveTypeIdAsync(ProductTypeCategory);
                await _vehicleTypeRepo.UpdateAsync(entity);
                row.ModelName = name;
                row.Remark = remark;
                row.TypeId = entity.TypeId;
                NlogHelper.Info($"型号管理·已保存 Type 表：Id={entity.Id} {entity.Name}");
            }
            catch (Exception ex)
            {
                NlogHelper.Info($"型号保存失败：{ex.Message}");
                if (owner != null)
                    await new InfoNoticeWindow("修改失败！").ShowDialog(owner);
                return;
            }
        }

        _ = oldModelName; // 型号改名不再联动 Para.ini 参数节

        LogMain("使用「用户管理·型号管理·更改」");
        if (changedModel.Count > 0)
            LogMain($"修改参数「{string.Join("、", changedModel)}」");

        _snapModelCategory = cat;
        _snapModelName = name;
        _snapModelRemark = remark;

        if (owner != null)
            await new InfoNoticeWindow("已保存").ShowDialog(owner);
    }

    private void ClearParameterFields()
    {
        _parameterEditingIniSection = null;
        ParamDurationText = "";

        ParamTargetPressureText = "";
        ParamDisplacementOffsetText = "";
        ParamDecelerationThresholdText = "";
        ParamHighPressureFloatLimitText = "";
        ParamReverseTimeText = "";
        ParamFastSlowSelectedIndex = 0;
    }

    private void LoadParaIniToParameterFields(ParaIniParameters para)
    {
        ParamDurationText = para.TestTime;

        ParamTargetPressureText = para.TargetPressure;
        ParamDisplacementOffsetText = para.DisplacementOffset;
        ParamDecelerationThresholdText = para.DecelerationThreshold;
        ParamHighPressureFloatLimitText = para.HighPressureFloatLimit;
        ParamReverseTimeText = para.DelayReliefTime;
        ParamFastSlowSelectedIndex = string.Equals(para.FastSlowSetting?.Trim(), "1", StringComparison.Ordinal)
            ? 1
            : 0;
    }

    private ParaIniParameters ReadParameterFieldsToParaIni() => new()
    {
        TestTime = ParamDurationText?.Trim() ?? "",

        TargetPressure = ParamTargetPressureText?.Trim() ?? "",
        DisplacementOffset = ParamDisplacementOffsetText?.Trim() ?? "",
        DecelerationThreshold = ParamDecelerationThresholdText?.Trim() ?? "",
        HighPressureFloatLimit = ParamHighPressureFloatLimitText?.Trim() ?? "",
        DelayReliefTime = ParamReverseTimeText?.Trim() ?? "",
        FastSlowSetting = ParamFastSlowSelectedIndex == 1 ? "1" : "0"
    };

    private static double ParseParamDouble(string? s, double fallback)
    {
        if (string.IsNullOrWhiteSpace(s))
            return fallback;
        return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
    }

    private static int ParseParamInt(string? s, int fallback)
    {
        if (string.IsNullOrWhiteSpace(s))
            return fallback;
        return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
    }

    [RelayCommand]
    private void LoadParameter()
    {
        var name = ParameterName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            NlogHelper.Info("参数管理·请输入参数节名称");
            return;
        }

        ParaIniFile.EnsureRunSection();
        var iniPath = ParaIniFile.ResolvePath();

        if (ParaIniFile.TryLoadSection(name, out var para, out var section, iniPath))
        {
            LoadParaIniToParameterFields(para);
            PushParaToMainUi(para);
            _parameterEditingIniSection = section;
            ParameterName = section;
            NlogHelper.Info($"参数管理·已从 Para.ini [{section}] 加载（{iniPath}）");
        }
        else
        {
            ClearParameterFields();
            _parameterEditingIniSection = name;
            ParameterName = name;
            NlogHelper.Info($"参数管理·[{name}] 不存在，填写后点保存可新建（{iniPath}）");
        }
    }

    [RelayCommand]
    private async Task SaveParameterAsync()
    {
        var owner = ResolveDialogOwner();
        var sectionToSave = ParameterName?.Trim();
        NlogHelper.Info($"参数管理·保存前 - ParameterName: '{ParameterName}'");

        if (string.IsNullOrEmpty(sectionToSave))
        {
            NlogHelper.Info("参数管理·请输入参数节名称");
            if (owner != null)
                await new InfoNoticeWindow("请输入参数节名称再保存。\r（例如：运行、测试A）").ShowDialog(owner);
            return;
        }

        try
        {
            var iniPath = ParaIniFile.ResolvePath();
            var para = ReadParameterFieldsToParaIni();
            ParaIniFile.SaveSection(sectionToSave, para, iniPath);
            _parameterEditingIniSection = sectionToSave;
            ParameterName = sectionToSave;
            PushParaToMainUi(para);
            LoadParameterNames();
            RefreshMainSections();
            LogMain("使用「用户管理·参数管理·保存」");
            NlogHelper.Info($"参数管理·已保存到 Para.ini [{sectionToSave}]（{iniPath}）");
            if (owner != null)
                await new InfoNoticeWindow($"已保存到 Para.ini\r[{sectionToSave}]\r{iniPath}").ShowDialog(owner);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"参数管理·Para.ini 保存失败：{ex.Message}");
            if (owner != null)
                await new InfoNoticeWindow($"保存失败：{ex.Message}").ShowDialog(owner);
        }
    }

    [RelayCommand]
    private async Task RestoreDefaultParametersAsync()
    {
        var owner = ResolveDialogOwner();
        if (_plc == null || !_plc.IsConnected)
        {
            if (owner != null)
                await new InfoNoticeWindow("PLC 未连接，无法恢复默认参数。").ShowDialog(owner);
            return;
        }

        if (owner == null)
            return;

        var confirmed = await ConfirmDialogWindow.ShowConfirmAsync(owner, "确认恢复默认参数吗？将脉冲触发 M403。", "恢复默认参数");
        if (!confirmed)
            return;

        try
        {
            await Task.Run(() =>
            {
                _plc.WriteTag("DI.MDI01", true);
                System.Threading.Thread.Sleep(500);
                _plc.WriteTag("DI.MDI01", false);
            });
            LogMain("使用「用户管理·参数管理·恢复默认参数」");
            NlogHelper.Info("参数管理·已触发恢复默认参数(M403)");
            await new InfoNoticeWindow("已触发恢复默认参数。").ShowDialog(owner);
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"参数管理·恢复默认参数失败: {ex.Message}");
            await new InfoNoticeWindow($"恢复默认参数失败：{ex.Message}").ShowDialog(owner);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var list = await _userRepo.GetAllAsync(includeDisabled: false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Users.Clear();
                foreach (var u in list)
                    Users.Add(UserListItemViewModel.FromEntity(u));
                AppendLocalDemoRowsIfEmpty();
                SelectedUser = Users.Count > 0 ? Users[0] : null;
            });
        }
        catch (System.Exception ex)
        {
            NlogHelper.Info($"用户列表加载失败：{ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Users.Clear();
                AppendLocalDemoRowsIfEmpty();
                SelectedUser = Users.Count > 0 ? Users[0] : null;
            });
        }
    }

    /// <summary>库无数据或加载失败时，仍显示两行模板（仅内存；保存会尝试写入库）</summary>
    private void AppendLocalDemoRowsIfEmpty()
    {
        if (Users.Count > 0) return;
        Users.Add(UserListItemViewModel.LocalDemo("Test1", "001", UserRole.Assistant));
        Users.Add(UserListItemViewModel.LocalDemo("Test2", "002", UserRole.Assistant));
    }

    [RelayCommand]
    private async Task AddUserAsync()
    {
        LogMain("使用「用户管理·用户列表·添加」");
        var owner = ResolveDialogOwner();
        if (owner == null)
        {
            NlogHelper.Info("用户管理·添加：未找到父窗口，无法弹出对话框");
            return;
        }

        try
        {
            var dialogVm = new UserEditDialogViewModel(_userRepo, existing: null);
            var win = new UserEditWindow { DataContext = dialogVm };
            dialogVm.RequestClose += () => { if (win.IsVisible) win.Close(); };
            await win.ShowDialog(owner);
            if (dialogVm.Saved)
                await RefreshAsync();
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"用户管理·添加窗口失败：{ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifySelection))]
    private async Task DeleteUserAsync()
    {
        if (SelectedUser == null) return;
        var owner = ResolveDialogOwner();
        if (owner == null)
        {
            NlogHelper.Info("用户管理·删除：未找到父窗口，无法弹出对话框");
            return;
        }

        var user = SelectedUser;
        var confirmed = await ConfirmDialogWindow.ShowConfirmAsync(owner,
            $"确定要删除用户「{user.UserName}」（工号 {user.JobNumber}）吗？",
            "确认删除");
        if (!confirmed) return;

        LogMain("使用「用户管理·用户列表·删除」");

        if (user.IsLocalDemo)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Users.Remove(user);
                SelectedUser = Users.FirstOrDefault();
            });
        }
        else
        {
            try
            {
                await _userRepo.DisableAsync(user.Id);
                NlogHelper.Info($"用户管理·已禁用：{user.JobNumber}");
                await RefreshAsync();
            }
            catch (System.Exception ex)
            {
                NlogHelper.Info($"用户删除失败：{ex.Message}");
                return;
            }
        }

        var done = new InfoNoticeWindow("删除成功");
        await done.ShowDialog(owner);
    }

    private bool CanModifySelection() => SelectedUser != null;

    [RelayCommand(CanExecute = nameof(CanModifySelection))]
    private async Task ChangeUserAsync()
    {
        if (SelectedUser == null) return;
        LogMain("使用「用户管理·用户列表·更改」");
        var owner = ResolveDialogOwner();
        if (owner == null)
        {
            NlogHelper.Info("用户管理·更改：未找到父窗口，无法弹出对话框");
            return;
        }

        try
        {
            var dialogVm = new UserEditDialogViewModel(_userRepo, SelectedUser);
            var win = new UserEditWindow { DataContext = dialogVm };
            dialogVm.RequestClose += () => { if (win.IsVisible) win.Close(); };
            await win.ShowDialog(owner);
            if (dialogVm.Saved)
                await RefreshAsync();
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"用户管理·更改窗口失败：{ex.Message}");
        }
    }

    private void ClearInspectorEditFields()
    {
        _suppressInspectorEditPush = true;
        try
        {
            EditAssistJob1 = "";
            EditAssistName1 = "";
            EditAssistJob2 = "";
            EditAssistName2 = "";
            EditAssistJob3 = "";
            EditAssistName3 = "";
            EditAssistJob4 = "";
            EditAssistName4 = "";
            EditMutualJob = "";
            EditMutualName = "";
            EditQualityJob = "";
            EditQualityName = "";
        }
        finally
        {
            _suppressInspectorEditPush = false;
        }
    }

    private void LoadInspectorEditFromRow(InspectorListItemViewModel row)
    {
        _suppressInspectorEditPush = true;
        try
        {
            EditAssistJob1 = row.AssistJob1;
            EditAssistName1 = row.AssistName1;
            EditAssistJob2 = row.AssistJob2;
            EditAssistName2 = row.AssistName2;
            EditAssistJob3 = row.AssistJob3;
            EditAssistName3 = row.AssistName3;
            EditAssistJob4 = row.AssistJob4;
            EditAssistName4 = row.AssistName4;
            EditMutualJob = row.MutualJob;
            EditMutualName = row.MutualName;
            EditQualityJob = row.QualityJob;
            EditQualityName = row.QualityName;
        }
        finally
        {
            _suppressInspectorEditPush = false;
        }
    }

    private void CaptureInspectorEditBaseline(InspectorListItemViewModel? row)
    {
        if (row == null)
        {
            _inspSnapJob1 = _inspSnapName1 = _inspSnapJob2 = _inspSnapName2 = "";
            _inspSnapJob3 = _inspSnapName3 = _inspSnapJob4 = _inspSnapName4 = "";
            _inspSnapMutualJob = _inspSnapMutualName = _inspSnapQualityJob = _inspSnapQualityName = "";
            return;
        }

        _inspSnapJob1 = row.AssistJob1;
        _inspSnapName1 = row.AssistName1;
        _inspSnapJob2 = row.AssistJob2;
        _inspSnapName2 = row.AssistName2;
        _inspSnapJob3 = row.AssistJob3;
        _inspSnapName3 = row.AssistName3;
        _inspSnapJob4 = row.AssistJob4;
        _inspSnapName4 = row.AssistName4;
        _inspSnapMutualJob = row.MutualJob;
        _inspSnapMutualName = row.MutualName;
        _inspSnapQualityJob = row.QualityJob;
        _inspSnapQualityName = row.QualityName;
    }

    private void PushInspectorEditsToSelectedRow()
    {
        if (_suppressInspectorEditPush || SelectedInspector == null) return;
        var row = SelectedInspector;
        row.AssistJob1 = EditAssistJob1 ?? "";
        row.AssistName1 = EditAssistName1 ?? "";
        row.AssistJob2 = EditAssistJob2 ?? "";
        row.AssistName2 = EditAssistName2 ?? "";
        row.AssistJob3 = EditAssistJob3 ?? "";
        row.AssistName3 = EditAssistName3 ?? "";
        row.AssistJob4 = EditAssistJob4 ?? "";
        row.AssistName4 = EditAssistName4 ?? "";
        row.MutualJob = EditMutualJob ?? "";
        row.MutualName = EditMutualName ?? "";
        row.QualityJob = EditQualityJob ?? "";
        row.QualityName = EditQualityName ?? "";
    }

    private void ApplyInspectorEditToRow(InspectorListItemViewModel row)// 注意：即使是本地行，也允许保存时自动去除前后空白
    {
        row.AssistJob1 = (EditAssistJob1 ?? "").Trim();// 注意：即使是本地行，也允许保存时自动去除前后空白
        row.AssistName1 = (EditAssistName1 ?? "").Trim();
        row.AssistJob2 = (EditAssistJob2 ?? "").Trim();
        row.AssistName2 = (EditAssistName2 ?? "").Trim();
        row.AssistJob3 = (EditAssistJob3 ?? "").Trim();
        row.AssistName3 = (EditAssistName3 ?? "").Trim();
        row.AssistJob4 = (EditAssistJob4 ?? "").Trim();
        row.AssistName4 = (EditAssistName4 ?? "").Trim();
        row.MutualJob = (EditMutualJob ?? "").Trim();
        row.MutualName = (EditMutualName ?? "").Trim();
        row.QualityJob = (EditQualityJob ?? "").Trim();
        row.QualityName = (EditQualityName ?? "").Trim();
    }

    private static void CopyRowToEntity(InspectorListItemViewModel row, InspectionStaffEntity e)// 注意：此方法仅用于将数据库记录加载到编辑区，不自动去除前后空白（保持与数据库一致），保存时会再次去除
    {
        e.AssistJob1 = row.AssistJob1;
        e.AssistName1 = row.AssistName1;
        e.AssistJob2 = row.AssistJob2;
        e.AssistName2 = row.AssistName2;
        e.AssistJob3 = row.AssistJob3;
        e.AssistName3 = row.AssistName3;
        e.AssistJob4 = row.AssistJob4;
        e.AssistName4 = row.AssistName4;
        e.MutualJob = row.MutualJob;
        e.MutualName = row.MutualName;
        e.QualityJob = row.QualityJob;
        e.QualityName = row.QualityName;
    }

    /// <summary>将底部录入区字段写入实体（与老版 ucUserSelection 新增逻辑一致）。</summary>
    private void ApplyEditFieldsToEntity(InspectionStaffEntity e)
    {
        e.AssistJob1 = (EditAssistJob1 ?? "").Trim();
        e.AssistName1 = (EditAssistName1 ?? "").Trim();
        e.AssistJob2 = (EditAssistJob2 ?? "").Trim();
        e.AssistName2 = (EditAssistName2 ?? "").Trim();
        e.AssistJob3 = (EditAssistJob3 ?? "").Trim();
        e.AssistName3 = (EditAssistName3 ?? "").Trim();
        e.AssistJob4 = (EditAssistJob4 ?? "").Trim();
        e.AssistName4 = (EditAssistName4 ?? "").Trim();
        e.MutualJob = (EditMutualJob ?? "").Trim();
        e.MutualName = (EditMutualName ?? "").Trim();
        e.QualityJob = (EditQualityJob ?? "").Trim();
        e.QualityName = (EditQualityName ?? "").Trim();
    }

    private InspectionStaffEntity BuildInspectorEntityFromEditFields()
    {
        var entity = new InspectionStaffEntity();
        ApplyEditFieldsToEntity(entity);
        return entity;
    }

    private InspectorListItemViewModel BuildInspectorRowFromEditFields(int id)
    {
        var row = InspectorListItemViewModel.LocalDemo(
            id,
            (EditAssistJob1 ?? "").Trim(),
            (EditAssistName1 ?? "").Trim(),
            (EditAssistJob2 ?? "").Trim(),
            (EditAssistName2 ?? "").Trim(),
            (EditAssistJob3 ?? "").Trim(),
            (EditAssistName3 ?? "").Trim(),
            (EditAssistJob4 ?? "").Trim(),
            (EditAssistName4 ?? "").Trim(),
            (EditMutualJob ?? "").Trim(),
            (EditMutualName ?? "").Trim(),
            (EditQualityJob ?? "").Trim(),
            (EditQualityName ?? "").Trim());
        return row;
    }

    [RelayCommand]
    private async Task RefreshInspectorsAsync()
    {
        try
        {
            var list = await _inspectorStaffRepo.GetAllAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Inspectors.Clear();
                foreach (var e in list)
                    Inspectors.Add(InspectorListItemViewModel.FromEntity(e));
                AppendDemoInspectorsIfEmpty();
                SelectedInspector = Inspectors.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"检查人员列表加载失败：{ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Inspectors.Clear();
                AppendDemoInspectorsIfEmpty();
                SelectedInspector = Inspectors.FirstOrDefault();
            });
        }
    }

    private void AppendDemoInspectorsIfEmpty()
    {
        if (Inspectors.Count > 0) return;
        Inspectors.Add(InspectorListItemViewModel.LocalDemo(-1, "010502790448", "祝帅"));
    }

    [RelayCommand]
    private async Task AddInspectorAsync()
    {
        LogMain("使用「用户管理·检查人员·新增」");
        var owner = ResolveDialogOwner();
        try
        {
            var entity = BuildInspectorEntityFromEditFields();
            var newId = await _inspectorStaffRepo.InsertAsync(entity);
            if (newId < 1)
            {
                NlogHelper.Info("检查人员·新增失败：未写入数据库");
                if (owner != null)
                    await new InfoNoticeWindow("新增失败！").ShowDialog(owner);
                return;
            }

            NlogHelper.Info("检查人员·已新增一条");
            await RefreshInspectorsAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SelectedInspector = Inspectors.LastOrDefault();
                if (SelectedInspector != null)
                {
                    LoadInspectorEditFromRow(SelectedInspector);
                    CaptureInspectorEditBaseline(SelectedInspector);
                }
            });
            if (owner != null)
                await new InfoNoticeWindow("新增成功！").ShowDialog(owner);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"检查人员新增失败（本地演示）：{ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var nextId = Inspectors.Where(i => i.Id < 0).Select(i => i.Id).DefaultIfEmpty(0).Min() - 1;
                var row = BuildInspectorRowFromEditFields(nextId);
                Inspectors.Add(row);
                SelectedInspector = row;
                CaptureInspectorEditBaseline(row);
            });
            if (owner != null)
                await new InfoNoticeWindow("新增成功！（本地演示，未连接数据库）").ShowDialog(owner);
        }
    }

    private bool CanModifyInspectorSelection() => SelectedInspector != null;

    [RelayCommand(CanExecute = nameof(CanModifyInspectorSelection))]
    private async Task DeleteInspectorAsync()
    {
        if (SelectedInspector == null) return;
        var owner = ResolveDialogOwner();
        if (owner == null)
        {
            NlogHelper.Info("检查人员·删除：未找到父窗口");
            return;
        }

        var row = SelectedInspector;
        var confirmed = await ConfirmDialogWindow.ShowConfirmAsync(owner, "确定要删除该条检查人员配置吗？", "确认删除");
        if (!confirmed) return;

        LogMain("使用「用户管理·检查人员·删除」");

        if (row.IsLocalDemo)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Inspectors.Remove(row);
                SelectedInspector = Inspectors.FirstOrDefault();
            });
        }
        else
        {
            try
            {
                await _inspectorStaffRepo.DeleteAsync(row.Id);
                await RefreshInspectorsAsync();
            }
            catch (Exception ex)
            {
                NlogHelper.Info($"检查人员删除失败：{ex.Message}");
                return;
            }
        }

        await new InfoNoticeWindow("删除成功").ShowDialog(owner);
    }

    [RelayCommand(CanExecute = nameof(CanModifyInspectorSelection))]
    private async Task ChangeInspectorAsync()
    {
        if (SelectedInspector == null) return;
        var owner = ResolveDialogOwner();

        var row = SelectedInspector;
        var changedInspector = new List<string>();
        void AddIf(string label, string edit, string rowVal)
        {
            if (!string.Equals((edit ?? "").Trim(), (rowVal ?? "").Trim(), StringComparison.Ordinal))
                changedInspector.Add(label);
        }

        AddIf("辅助人工号1", EditAssistJob1, _inspSnapJob1);
        AddIf("辅助人姓名1", EditAssistName1, _inspSnapName1);
        AddIf("辅助人工号2", EditAssistJob2, _inspSnapJob2);
        AddIf("辅助人姓名2", EditAssistName2, _inspSnapName2);
        AddIf("辅助人工号3", EditAssistJob3, _inspSnapJob3);
        AddIf("辅助人姓名3", EditAssistName3, _inspSnapName3);
        AddIf("辅助人工号4", EditAssistJob4, _inspSnapJob4);
        AddIf("辅助人姓名4", EditAssistName4, _inspSnapName4);
        AddIf("互检人工号", EditMutualJob, _inspSnapMutualJob);
        AddIf("互检人姓名", EditMutualName, _inspSnapMutualName);
        AddIf("质检人工号", EditQualityJob, _inspSnapQualityJob);
        AddIf("质检人姓名", EditQualityName, _inspSnapQualityName);

        LogMain("使用「用户管理·检查人员·修改」");
        if (changedInspector.Count > 0)
            LogMain($"修改参数「{string.Join("、", changedInspector)}」");

        ApplyInspectorEditToRow(row);

        if (row.IsLocalDemo)
        {
            NlogHelper.Info("检查人员·已更新（本地演示）");
        }
        else
        {
            try
            {
                var entity = await _inspectorStaffRepo.GetByIdAsync(row.Id);
                if (entity == null)
                {
                    NlogHelper.Info("检查人员·更改：记录不存在");
                    return;
                }

                CopyRowToEntity(row, entity);
                await _inspectorStaffRepo.UpdateAsync(entity);
                NlogHelper.Info($"检查人员·已保存：Id={row.Id}");
            }
            catch (Exception ex)
            {
                NlogHelper.Info($"检查人员保存失败：{ex.Message}");
                if (owner != null)
                    await new InfoNoticeWindow("修改失败！").ShowDialog(owner);
                return;
            }
        }

        LoadInspectorEditFromRow(row);
        CaptureInspectorEditBaseline(row);

        if (owner != null)
            await new InfoNoticeWindow("修改成功！").ShowDialog(owner);
    }

    private static string LegacyProtectionJsonPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PressureTestApp",
            "protection_parameters.json");

    private async Task LoadProtectionParametersAsync()
    {
        try
        {
            var iniPath = DeviceInspectConfig.ResolveFilePath();
            await Task.Run(() =>
            {
                if (!File.Exists(iniPath) && File.Exists(LegacyProtectionJsonPath))
                    TryMigrateLegacyJsonToIni(iniPath);
            });

            var cfg = await Task.Run(() => new DeviceInspectConfig());

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyDeviceInspectConfig(cfg);
                NlogHelper.Info($"保护参数·已从 INI 加载：{iniPath}");
            });

            //if (_plc != null && _plc.IsConnected)
            //{
            //    await LoadProtectionFromPlcAsync();
            //    NlogHelper.Info("保护参数·已从 PLC 加载");
            //}
            //else
            //{
            //    await Dispatcher.UIThread.InvokeAsync(() =>
            //    {
            //        ApplyDeviceInspectConfig(cfg);
            //        NlogHelper.Info($"保护参数·已从 INI 加载：{iniPath}");
            //    });
            //}
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"保护参数·加载失败，使用默认：{ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(ResetProtectionParametersToDefaults);
        }
    }

    private async Task LoadProtectionFromPlcAsync()
    {
        try
        {
            if (_plc == null || !_plc.IsConnected)
                return;

            var plcData = await Task.Run(() => _plc.GetPlcStatusList());
            if (plcData == null || plcData.Count == 0)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                //ProtEmergencyStop = GetPlcValueAsString(plcData, "PUB.P09", DeviceInspectConfig.DefaultThreshold);
                //ProtGroundAlarm = GetPlcValueAsString(plcData, "PUB.P10", DeviceInspectConfig.DefaultThreshold);
                //ProtBell = GetPlcValueAsString(plcData, "PUB.P11", DeviceInspectConfig.DefaultThreshold);
                //ProtDoorInterlock = GetPlcValueAsString(plcData, "PUB.P12", DeviceInspectConfig.DefaultThreshold);
                //ProtVoltageRegLow = GetPlcValueAsString(plcData, "PUB.P13", DeviceInspectConfig.DefaultThreshold);
                //ProtVoltageRegHigh = GetPlcValueAsString(plcData, "PUB.P14", DeviceInspectConfig.DefaultThreshold);
                //ProtVoltageRegMotorAlarm = GetPlcValueAsString(plcData, "PUB.P15", DeviceInspectConfig.DefaultThreshold);
                //ProtMainContactor = GetPlcValueAsString(plcData, "PUB.P16", DeviceInspectConfig.DefaultThreshold);
                //ProtSlaveContactor = GetPlcValueAsString(plcData, "PUB.P17", DeviceInspectConfig.DefaultThreshold);
                //ProtDischarge = GetPlcValueAsString(plcData, "PUB.P18", DeviceInspectConfig.DefaultThreshold);
                //ProtElectricDoorLock = GetPlcValueAsString(plcData, "PUB.P19", DeviceInspectConfig.DefaultThreshold);
                //ProtStartButton = GetPlcValueAsString(plcData, "PUB.P20", DeviceInspectConfig.DefaultThreshold);
                //ProtStepUpButton = GetPlcValueAsString(plcData, "PUB.P21", DeviceInspectConfig.DefaultThreshold);
                //ProtStepDownButton = GetPlcValueAsString(plcData, "PUB.P22", DeviceInspectConfig.DefaultThreshold);
                //ProtStopButton = GetPlcValueAsString(plcData, "PUB.P23", DeviceInspectConfig.DefaultThreshold);
                //ProtResetButton = GetPlcValueAsString(plcData, "PUB.P24", DeviceInspectConfig.DefaultThreshold);
                //ProtTimerButton = GetPlcValueAsString(plcData, "PUB.P25", DeviceInspectConfig.DefaultThreshold);
                //ProtCounter = GetPlcValueAsString(plcData, "PUB.P26", DeviceInspectConfig.DefaultThreshold);
                RefreshProtectionSnapshotForLog();
            });
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "保护参数·从 PLC 加载失败");
        }
    }

    private static string GetPlcValueAsString(List<PlcDataModel> plcData, string tagName, string defaultValue)
    {
        var item = plcData.FirstOrDefault(d => 
            string.Equals(d.TagName, tagName, StringComparison.OrdinalIgnoreCase));
        if (item != null && item.Value != null)
        {
            return item.Value.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <summary>旧版 JSON 仅迁移一次到 INI（若 INI 尚不存在）。</summary>
    private static void TryMigrateLegacyJsonToIni(string iniPath)// 注意：迁移时会将 JSON 中的数值转换为字符串写入 INI，且不进行任何合理性检查（保持与旧版一致），迁移完成后 JSON 不再使用但仍保留在磁盘上
    {
        try
        {
            var json = File.ReadAllText(LegacyProtectionJsonPath);
            var store = JsonSerializer.Deserialize<ProtectionParametersStore>(json);
            if (store == null) return;

            var cfg = new DeviceInspectConfig();
            cfg.EmergencyStop = store.EmergencyStop.ToString(CultureInfo.InvariantCulture);
            cfg.Grounding = store.GroundAlarm.ToString(CultureInfo.InvariantCulture);
            cfg.ElectricBell = store.Bell.ToString(CultureInfo.InvariantCulture);
            cfg.Interlock = store.DoorInterlock.ToString(CultureInfo.InvariantCulture);
            cfg.LowerLimit = store.VoltageRegLow.ToString(CultureInfo.InvariantCulture);
            cfg.HighLimit = store.VoltageRegHigh.ToString(CultureInfo.InvariantCulture);
            cfg.ElectricalMachinery = store.VoltageRegMotorAlarm.ToString(CultureInfo.InvariantCulture);
            cfg.MainContact = store.MainContactor.ToString(CultureInfo.InvariantCulture);
            cfg.FromContact = store.SlaveContactor.ToString(CultureInfo.InvariantCulture);
            cfg.Discharge = store.Discharge.ToString(CultureInfo.InvariantCulture);
            cfg.DoorLock = store.ElectricDoorLock.ToString(CultureInfo.InvariantCulture);
            cfg.Start = store.StartButton.ToString(CultureInfo.InvariantCulture);
            cfg.Boost = store.StepUpButton.ToString(CultureInfo.InvariantCulture);
            cfg.StepDown = store.StepDownButton.ToString(CultureInfo.InvariantCulture);
            cfg.Stop = store.StopButton.ToString(CultureInfo.InvariantCulture);
            cfg.Reset = store.ResetButton.ToString(CultureInfo.InvariantCulture);
            cfg.TimeBtn = store.TimerButton.ToString(CultureInfo.InvariantCulture);
            cfg.TimerWare = store.Counter.ToString(CultureInfo.InvariantCulture);
            cfg.Save();
            NlogHelper.Info($"保护参数·已从旧 JSON 迁移到：{iniPath}");
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"保护参数·JSON 迁移失败：{ex.Message}");
        }
    }

    private void ApplyDeviceInspectConfig(DeviceInspectConfig cfg)
    {
        ProtEmergencyStop = cfg.EmergencyStop;
        ProtGroundAlarm = cfg.Grounding;
        ProtBell = cfg.ElectricBell;
        ProtDoorInterlock = cfg.Interlock;
        ProtVoltageRegLow = cfg.LowerLimit;
        ProtVoltageRegHigh = cfg.HighLimit;
        ProtVoltageRegMotorAlarm = cfg.ElectricalMachinery;
        ProtMainContactor = cfg.MainContact;
        ProtSlaveContactor = cfg.FromContact;
        ProtDischarge = cfg.Discharge;
        ProtElectricDoorLock = cfg.DoorLock;
        ProtStartButton = cfg.Start;
        ProtStepUpButton = cfg.Boost;
        ProtStepDownButton = cfg.StepDown;
        ProtStopButton = cfg.Stop;
        ProtResetButton = cfg.Reset;
        ProtTimerButton = cfg.TimeBtn;
        ProtCounter = cfg.TimerWare;
        RefreshProtectionSnapshotForLog();
    }

    private readonly record struct ProtectionFieldTexts(
        string EmergencyStop,
        string Grounding,
        string ElectricBell,
        string Interlock,
        string LowerLimit,
        string HighLimit,
        string ElectricalMachinery,
        string MainContact,
        string FromContact,
        string Discharge,
        string DoorLock,
        string Start,
        string Boost,
        string StepDown,
        string Stop,
        string Reset,
        string TimeBtn,
        string TimerWare);

    /// <summary>在 UI 线程抓取当前录入值（保存前调用，避免绑定未提交或后台线程读不到）。</summary>
    private ProtectionFieldTexts CaptureProtectionFieldTexts() =>
        new(
            (ProtEmergencyStop ?? "").Trim(),
            (ProtGroundAlarm ?? "").Trim(),
            (ProtBell ?? "").Trim(),
            (ProtDoorInterlock ?? "").Trim(),
            (ProtVoltageRegLow ?? "").Trim(),
            (ProtVoltageRegHigh ?? "").Trim(),
            (ProtVoltageRegMotorAlarm ?? "").Trim(),
            (ProtMainContactor ?? "").Trim(),
            (ProtSlaveContactor ?? "").Trim(),
            (ProtDischarge ?? "").Trim(),
            (ProtElectricDoorLock ?? "").Trim(),
            (ProtStartButton ?? "").Trim(),
            (ProtStepUpButton ?? "").Trim(),
            (ProtStepDownButton ?? "").Trim(),
            (ProtStopButton ?? "").Trim(),
            (ProtResetButton ?? "").Trim(),
            (ProtTimerButton ?? "").Trim(),
            (ProtCounter ?? "").Trim());

    private static void ApplyProtectionFieldTexts(DeviceInspectConfig cfg, ProtectionFieldTexts t)
    {
        cfg.EmergencyStop = t.EmergencyStop;
        cfg.Grounding = t.Grounding;
        cfg.ElectricBell = t.ElectricBell;
        cfg.Interlock = t.Interlock;
        cfg.LowerLimit = t.LowerLimit;
        cfg.HighLimit = t.HighLimit;
        cfg.ElectricalMachinery = t.ElectricalMachinery;
        cfg.MainContact = t.MainContact;
        cfg.FromContact = t.FromContact;
        cfg.Discharge = t.Discharge;
        cfg.DoorLock = t.DoorLock;
        cfg.Start = t.Start;
        cfg.Boost = t.Boost;
        cfg.StepDown = t.StepDown;
        cfg.Stop = t.Stop;
        cfg.Reset = t.Reset;
        cfg.TimeBtn = t.TimeBtn;
        cfg.TimerWare = t.TimerWare;
    }

    /// <summary>从 INI 重新加载（有文件则用客户配置；无文件/缺键才用出厂默认 10000）。</summary>
    private void ResetProtectionParametersToDefaults() =>
        ApplyDeviceInspectConfig(new DeviceInspectConfig());

    private void RefreshProtectionSnapshotForLog() =>
        _protectionSnapshotForLog = BuildProtectionStoreFromFields();

    private static List<string> CollectProtectionFieldChanges(ProtectionParametersStore? before, ProtectionParametersStore after)
    {
        var list = new List<string>();
        if (before == null) return list;
        void AddIf(string label, int oldV, int newV)
        {
            if (oldV != newV) list.Add(label);
        }

        AddIf("急停计数", before.EmergencyStop, after.EmergencyStop);
        AddIf("接地报警计数", before.GroundAlarm, after.GroundAlarm);
        AddIf("电铃计数", before.Bell, after.Bell);
        AddIf("门联锁计数", before.DoorInterlock, after.DoorInterlock);
        AddIf("调压底限计数", before.VoltageRegLow, after.VoltageRegLow);
        AddIf("调压高限计数", before.VoltageRegHigh, after.VoltageRegHigh);
        AddIf("调压电机报警计数", before.VoltageRegMotorAlarm, after.VoltageRegMotorAlarm);
        AddIf("主接触器计数", before.MainContactor, after.MainContactor);
        AddIf("从接触器计数", before.SlaveContactor, after.SlaveContactor);
        AddIf("放电计数", before.Discharge, after.Discharge);
        AddIf("电动门锁计数", before.ElectricDoorLock, after.ElectricDoorLock);
        AddIf("启动按钮计数", before.StartButton, after.StartButton);
        AddIf("升压按钮计数", before.StepUpButton, after.StepUpButton);
        AddIf("降压按钮计数", before.StepDownButton, after.StepDownButton);
        AddIf("停止按钮计数", before.StopButton, after.StopButton);
        AddIf("复位按钮计数", before.ResetButton, after.ResetButton);
        AddIf("计时按钮计数", before.TimerButton, after.TimerButton);
        AddIf("计数器计数", before.Counter, after.Counter);
        return list;
    }

    private void ApplyProtectionStore(ProtectionParametersStore s)
    {
        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        ProtEmergencyStop = I(s.EmergencyStop);
        ProtGroundAlarm = I(s.GroundAlarm);
        ProtBell = I(s.Bell);
        ProtDoorInterlock = I(s.DoorInterlock);
        ProtVoltageRegLow = I(s.VoltageRegLow);
        ProtVoltageRegHigh = I(s.VoltageRegHigh);
        ProtVoltageRegMotorAlarm = I(s.VoltageRegMotorAlarm);
        ProtMainContactor = I(s.MainContactor);
        ProtSlaveContactor = I(s.SlaveContactor);
        ProtDischarge = I(s.Discharge);
        ProtElectricDoorLock = I(s.ElectricDoorLock);
        ProtStartButton = I(s.StartButton);
        ProtStepUpButton = I(s.StepUpButton);
        ProtStepDownButton = I(s.StepDownButton);
        ProtStopButton = I(s.StopButton);
        ProtResetButton = I(s.ResetButton);
        ProtTimerButton = I(s.TimerButton);
        ProtCounter = I(s.Counter);
        RefreshProtectionSnapshotForLog();
    }

    private ProtectionParametersStore BuildProtectionStoreFromFields() =>
        new()
        {
            EmergencyStop = ParseProtectionCount(ProtEmergencyStop),
            GroundAlarm = ParseProtectionCount(ProtGroundAlarm),
            Bell = ParseProtectionCount(ProtBell),
            DoorInterlock = ParseProtectionCount(ProtDoorInterlock),
            VoltageRegLow = ParseProtectionCount(ProtVoltageRegLow),
            VoltageRegHigh = ParseProtectionCount(ProtVoltageRegHigh),
            VoltageRegMotorAlarm = ParseProtectionCount(ProtVoltageRegMotorAlarm),
            MainContactor = ParseProtectionCount(ProtMainContactor),
            SlaveContactor = ParseProtectionCount(ProtSlaveContactor),
            Discharge = ParseProtectionCount(ProtDischarge),
            ElectricDoorLock = ParseProtectionCount(ProtElectricDoorLock),
            StartButton = ParseProtectionCount(ProtStartButton),
            StepUpButton = ParseProtectionCount(ProtStepUpButton),
            StepDownButton = ParseProtectionCount(ProtStepDownButton),
            StopButton = ParseProtectionCount(ProtStopButton),
            ResetButton = ParseProtectionCount(ProtResetButton),
            TimerButton = ParseProtectionCount(ProtTimerButton),
            Counter = ParseProtectionCount(ProtCounter)
        };

    private static int ParseProtectionCount(string? text)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return n;
        return int.TryParse(DeviceInspectConfig.DefaultThreshold, out var def) ? def : 0;
    }

    [RelayCommand]
    private async Task SaveProtectionParametersAsync()// 注意：保存时会自动去除前后空白，且对于非整数输入会使用默认值（保持与旧版 INI 一致的宽容度），但不会弹窗提示错误输入；保存完成后会刷新日志快照基线
    {
        var owner = ResolveDialogOwner();
        try
        {
            var fields = CaptureProtectionFieldTexts();
            var store = BuildProtectionStoreFromFields();
            var changedProt = CollectProtectionFieldChanges(_protectionSnapshotForLog, store);
            LogMain("使用「用户管理·保护参数·保存」");
            if (changedProt.Count > 0)
                LogMain($"修改参数「{string.Join("、", changedProt)}」");

            var iniPath = DeviceInspectConfig.ResolveFilePath();// 注意：保存时再次解析路径，确保日志中记录的路径与实际写入的文件一致（可能受环境变量等因素影响）
            await Task.Run(() =>
            {
                //var cfg = new DeviceInspectConfig(loadOnCreate: false);
                var cfg = new DeviceInspectConfig();
                ApplyProtectionFieldTexts(cfg, fields);
                cfg.Save();
            });

            // 写入 PLC
            if (_plc != null && _plc.IsConnected)
            {
                await SaveProtectionToPlcAsync(store);
            }

            _protectionSnapshotForLog = store;
            NlogHelper.Info($"保护参数·已保存到：{iniPath}");
            if (owner != null)
                await new InfoNoticeWindow($"保存成功！\r{(_plc?.IsConnected == true ? "\n已同步到 PLC" : "")}").ShowDialog(owner);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"保护参数·保存失败：{ex.Message}");
            if (owner != null)
                await new InfoNoticeWindow($"保存失败：{ex.Message}").ShowDialog(owner);
        }
    }

    private async Task SaveProtectionToPlcAsync(ProtectionParametersStore store)
    {
        try
        {
            if (_plc == null || !_plc.IsConnected)
                return;

            await Task.Run(() =>
            {
                _plc.WriteTag("PUB.P09", store.EmergencyStop);
                _plc.WriteTag("PUB.P10", store.GroundAlarm);
                _plc.WriteTag("PUB.P11", store.Bell);
                _plc.WriteTag("PUB.P12", store.DoorInterlock);
                _plc.WriteTag("PUB.P13", store.VoltageRegLow);
                _plc.WriteTag("PUB.P14", store.VoltageRegHigh);
                _plc.WriteTag("PUB.P15", store.VoltageRegMotorAlarm);
                _plc.WriteTag("PUB.P16", store.MainContactor);
                _plc.WriteTag("PUB.P17", store.SlaveContactor);
                _plc.WriteTag("PUB.P18", store.Discharge);
                _plc.WriteTag("PUB.P19", store.ElectricDoorLock);
                _plc.WriteTag("PUB.P20", store.StartButton);
                _plc.WriteTag("PUB.P21", store.StepUpButton);
                _plc.WriteTag("PUB.P22", store.StepDownButton);
                _plc.WriteTag("PUB.P23", store.StopButton);
                _plc.WriteTag("PUB.P24", store.ResetButton);
                _plc.WriteTag("PUB.P25", store.TimerButton);
                _plc.WriteTag("PUB.P26", store.Counter);
            });

            NlogHelper.Info("保护参数·已同步到 PLC");
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "保护参数·写入 PLC 失败");
        }
    }

    private async Task LoadCommunicationParametersAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var config = LoadCommunicationConfigFromAppSettings();

                Dispatcher.UIThread.Invoke(() =>
                {
                    var tempHumiPort = config.TempHumiConfig?.PortName ?? "COM1";
                    var insulationPort = config.InsulationConfig?.PortName ?? "COM2";
                    var turnPort = config.TurnConfig?.PortName ?? "COM3";
                    
                    
                    
                    //LoadComPorts();

                    CommTempHumiPort = tempHumiPort;
                    CommInsulationPort = insulationPort;
                    CommTurnPort = turnPort;


                    CommPlcEnabled = config.Plc200Config?.Enabled ?? true;
                    CommPlcIp = config.Plc200Config?.IpAddress ?? "192.168.0.3";
                    CommPlcCsvFilePath = config.Plc200Config?.CsvFilePath ?? "ALL_PLC_XDP.csv";
                    CommPlcPort = "102";
                    CommTempHumiBaudRate = (config.TempHumiConfig?.BaudRate ?? 9600).ToString();
                    CommInsulationBaudRate = (config.InsulationConfig?.BaudRate ?? 9600).ToString();
                    CommTurnBaudRate = (config.TurnConfig?.BaudRate ?? 9600).ToString();
                });
            });
            NlogHelper.Info("通讯参数·已加载");
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"通讯参数·加载失败，使用默认：{ex.Message}");
        }
    }

    private AppSettings LoadCommunicationConfigFromAppSettings()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
        if (!File.Exists(configPath))
        {
            return CreateDefaultAppSettings();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            if (config != null)
            {
                return config;
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"解析 appsettings.json 失败: {ex.Message}");
        }
        
        return CreateDefaultAppSettings();
    }

    private AppSettings CreateDefaultAppSettings()
    {
        return new AppSettings
        {
            Plc200Config = new Plc200Config { Enabled = true, IpAddress = "192.168.0.3", CsvFilePath = "ALL_PLC_XDP.csv" },
            TempHumiConfig = new TempHumiConfig { Enabled = true, PortName = "COM1", BaudRate = 9600 },
            InsulationConfig = new InsulationConfig { Enabled = true, PortName = "COM2", BaudRate = 9600 },
            TurnConfig = new TurnConfig { Enabled = true, PortName = "COM3", BaudRate = 9600 }
        };
    }

    private void SaveCommunicationConfigToAppSettings(
        bool plcEnabled, string plcIp, string plcCsvFilePath,
        string tempHumiPort, string tempHumiBaudRate,
        string insulationPort, string insulationBaudRate,
        string turnPort, string turnBaudRate)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
        if (!File.Exists(configPath))
            return;

        var json = File.ReadAllText(configPath);
        var config = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        
        if (config.Plc200Config == null)
            config.Plc200Config = new Plc200Config();
        config.Plc200Config.Enabled = plcEnabled;
        config.Plc200Config.IpAddress = plcIp;
        config.Plc200Config.CsvFilePath = plcCsvFilePath;

        if (config.TempHumiConfig == null)
            config.TempHumiConfig = new TempHumiConfig();
        config.TempHumiConfig.PortName = tempHumiPort;
        if (int.TryParse(tempHumiBaudRate, out var tempHumiBaud))
            config.TempHumiConfig.BaudRate = tempHumiBaud;

        if (config.InsulationConfig == null)
            config.InsulationConfig = new InsulationConfig();
        config.InsulationConfig.PortName = insulationPort;
        if (int.TryParse(insulationBaudRate, out var insulationBaud))
            config.InsulationConfig.BaudRate = insulationBaud;

        if (config.TurnConfig == null)
            config.TurnConfig = new TurnConfig();
        config.TurnConfig.PortName = turnPort;
        if (int.TryParse(turnBaudRate, out var turnBaud))
            config.TurnConfig.BaudRate = turnBaud;

        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(configPath, System.Text.Json.JsonSerializer.Serialize(config, options));
    }

    private class AppSettings
    {
        public Plc200Config? Plc200Config { get; set; }
        public TempHumiConfig? TempHumiConfig { get; set; }
        public InsulationConfig? InsulationConfig { get; set; }
        public TurnConfig? TurnConfig { get; set; }
    }

    [RelayCommand]
    private async Task SaveCommunicationParametersAsync()
    {
        var owner = ResolveDialogOwner();
        try
        {
            LogMain("使用「用户管理·通讯管理·保存」");

            await Task.Run(() =>
            {
                SaveCommunicationConfigToAppSettings(
                    CommPlcEnabled,
                    CommPlcIp.Trim(),
                    CommPlcCsvFilePath.Trim(),
                    CommTempHumiPort.Trim(),
                    CommTempHumiBaudRate.Trim(),
                    CommInsulationPort.Trim(),
                    CommInsulationBaudRate.Trim(),
                    CommTurnPort.Trim(),
                    CommTurnBaudRate.Trim());
            });

            NlogHelper.Info("通讯参数·已保存");
            if (owner != null)
                await new InfoNoticeWindow("保存成功！").ShowDialog(owner);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"通讯参数·保存失败：{ex.Message}");
            if (owner != null)
                await new InfoNoticeWindow($"保存失败：{ex.Message}").ShowDialog(owner);
        }
    }

    private Window? ResolveDialogOwner()
    {
        if (HostWindow != null)
            return HostWindow;
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            && d.MainWindow is Window w)
            return w;
        return null;
    }
}

/// <summary>保护参数计数器配置（本地 JSON 持久化）。</summary>
internal sealed class ProtectionParametersStore
{
    public int EmergencyStop { get; set; }
    public int GroundAlarm { get; set; }
    public int Bell { get; set; }
    public int DoorInterlock { get; set; }
    public int VoltageRegLow { get; set; }
    public int VoltageRegHigh { get; set; }
    public int VoltageRegMotorAlarm { get; set; }
    public int MainContactor { get; set; }
    public int SlaveContactor { get; set; }
    public int Discharge { get; set; }
    public int ElectricDoorLock { get; set; }
    public int StartButton { get; set; }
    public int StepUpButton { get; set; }
    public int StepDownButton { get; set; }
    public int StopButton { get; set; }
    public int ResetButton { get; set; }
    public int TimerButton { get; set; }
    public int Counter { get; set; }
}

/// <summary>检查人员表格行（AuxiliaryTable 或本地演示）</summary>
public partial class InspectorListItemViewModel : ObservableObject
{
    public int Id { get; private init; }
    public bool IsLocalDemo { get; private init; }

    [ObservableProperty] private string _assistJob1 = "";
    [ObservableProperty] private string _assistName1 = "";
    [ObservableProperty] private string _assistJob2 = "";
    [ObservableProperty] private string _assistName2 = "";
    [ObservableProperty] private string _assistJob3 = "";
    [ObservableProperty] private string _assistName3 = "";
    [ObservableProperty] private string _assistJob4 = "";
    [ObservableProperty] private string _assistName4 = "";
    [ObservableProperty] private string _mutualJob = "";
    [ObservableProperty] private string _mutualName = "";
    [ObservableProperty] private string _qualityJob = "";
    [ObservableProperty] private string _qualityName = "";

    public static InspectorListItemViewModel FromEntity(InspectionStaffEntity e) => new()
    {
        Id = e.Id,
        IsLocalDemo = false,
        AssistJob1 = e.AssistJob1,
        AssistName1 = e.AssistName1,
        AssistJob2 = e.AssistJob2,
        AssistName2 = e.AssistName2,
        AssistJob3 = e.AssistJob3,
        AssistName3 = e.AssistName3,
        AssistJob4 = e.AssistJob4,
        AssistName4 = e.AssistName4,
        MutualJob = e.MutualJob,
        MutualName = e.MutualName,
        QualityJob = e.QualityJob,
        QualityName = e.QualityName
    };

    public static InspectorListItemViewModel LocalDemo(
        int id,
        string assistJob1,
        string assistName1,
        string assistJob2 = "",
        string assistName2 = "",
        string assistJob3 = "",
        string assistName3 = "",
        string assistJob4 = "",
        string assistName4 = "",
        string mutualJob = "",
        string mutualName = "",
        string qualityJob = "",
        string qualityName = "") => new()
    {
        Id = id,
        IsLocalDemo = true,
        AssistJob1 = assistJob1,
        AssistName1 = assistName1,
        AssistJob2 = assistJob2,
        AssistName2 = assistName2,
        AssistJob3 = assistJob3,
        AssistName3 = assistName3,
        AssistJob4 = assistJob4,
        AssistName4 = assistName4,
        MutualJob = mutualJob,
        MutualName = mutualName,
        QualityJob = qualityJob,
        QualityName = qualityName
    };
}

/// <summary>型号管理列表行（Type 表或本地演示）</summary>
public partial class ProductModelRowViewModel : ObservableObject
{
    public int Id { get; private init; }
    public bool IsLocalDemo { get; private init; }
    public int TypeId { get; set; }

    [ObservableProperty] private string _modelName = "";

    [ObservableProperty] private string _remark = "";

    public static ProductModelRowViewModel FromEntity(VehicleTypeEntity t) => new()
    {
        Id = t.Id,
        IsLocalDemo = false,
        TypeId = t.TypeId,
        ModelName = t.Name,
        Remark = t.Mark ?? ""
    };

    public static ProductModelRowViewModel LocalDemo(int id, string name, string remark) => new()
    {
        Id = id,
        IsLocalDemo = true,
        TypeId = 1,
        ModelName = name,
        Remark = remark
    };
}

public partial class UserListItemViewModel : ObservableObject
{
    public int Id { get; private set; }

    public UserRole Role { get; private set; }

    /// <summary>库未加载时的界面占位行，删除仅从列表移除</summary>
    public bool IsLocalDemo { get; private set; }

    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private string _jobNumber = "";
    [ObservableProperty] private string _roleDisplay = "";
    [ObservableProperty] private string _teamName = "";

    public static UserListItemViewModel FromEntity(UserEntity u) => new()
    {
        Id = u.Id,
        Role = u.Role,
        IsLocalDemo = false,
        UserName = u.Name,
        JobNumber = u.JobNumber,
        RoleDisplay = UserManagementViewModel.RoleDisplay(u.Role),
        TeamName = string.IsNullOrWhiteSpace(u.DepName) ? "—" : u.DepName!
    };

    public static UserListItemViewModel LocalDemo(string name, string jobNumber, UserRole role) => new()
    {
        Id = 0,
        IsLocalDemo = true,
        Role = role,
        UserName = name,
        JobNumber = jobNumber,
        RoleDisplay = UserManagementViewModel.RoleDisplay(role),
        TeamName = "—"
    };
}
