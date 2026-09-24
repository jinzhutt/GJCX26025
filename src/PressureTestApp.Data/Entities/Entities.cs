using FreeSql.DataAnnotations;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;

namespace PressureTestApp.Data.Entities;

/// <summary>
/// 旧库用户表 Users（工号作盐 + SHA512×1024 + Base64 存 Password 列）
/// </summary>
[Table(Name = "Users")]
public class UserEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(Name = "UserName", StringLength = 64, IsNullable = false)]
    public string Name { get; set; } = "";

    [Column(Name = "Password", StringLength = 256)]
    public string PasswordHash { get; set; } = "";

    [Column(Name = "JobNumber", StringLength = 32, IsNullable = false)]
    public string JobNumber { get; set; } = "";

    [Column(Name = "Permission", StringLength = 32)]
    public string Permission { get; set; } = "操作员";

    [Column(Name = "Sort")]
    public int Sort { get; set; }

    [Column(Name = "Enable")]
    public int Enable { get; set; } = 1;

    [Column(Name = "DepId", StringLength = 64)]
    public string? DepId { get; set; }

    [Column(Name = "DepName", StringLength = 128)]
    public string? DepName { get; set; }

    [Column(Name = "CreateDateTime")]
    public DateTime? CreateDateTime { get; set; }

    [Column(Name = "UpdateDateTime")]
    public DateTime? UpdateDateTime { get; set; }

    [Column(Name = "LastLoginTime")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>程序内角色（映射 Permission，不落库）</summary>
    [Column(IsIgnore = true)]
    public UserRole Role
    {
        get => UserPermissionMapper.PermissionToRole(Permission);
        set => Permission = UserPermissionMapper.RoleToPermission(value);
    }

    [Column(IsIgnore = true)]
    public bool Enabled
    {
        get => Enable == 1;
        set => Enable = value ? 1 : 0;
    }
}

/// <summary>
/// 车型（API findAllProjectList）
/// </summary>
[Table(Name = "T_PROJECT")]
public class ProjectEntity
{
    [Column(IsPrimary = true)]
    public int ProjectId { get; set; }

    [Column(StringLength = 16)]
    public string? ProjectType { get; set; }            // E/C/D/K

    [Column(StringLength = 32)]
    public string? ProjectNumber { get; set; }          // E32C

    [Column(StringLength = 256)]
    public string? ProjectName { get; set; }            // CR400AF...

    [Column(StringLength = 64)]
    public string? WbsCode { get; set; }

    [Column(StringLength = 64)]
    public string? ProjectNumberAll { get; set; }

    /// <summary>车辆名称列表，逗号分隔（TC01,M02,...）</summary>
    [Column(StringLength = 1024)]
    public string? VehicleName { get; set; }

    public DateTime SyncedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 车列（API findAllTrainList）
/// </summary>
[Table(Name = "T_TRAIN")]
public class TrainEntity
{
    [Column(IsPrimary = true)]
    public int Id { get; set; }

    [Column(StringLength = 32)]
    public string? TrainNo { get; set; }                // 新造列号

    [Column(StringLength = 32)]
    public string? TrainCode { get; set; }              // 配属列号

    public int ProjectId { get; set; }

    public DateTime SyncedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 数据设置 · 型号管理（TestBed · Type 表）
/// </summary>
[Table(Name = "Type")]
public class VehicleTypeEntity
{
    [Column(Name = "ID", IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(Name = "Name")] public string Name { get; set; } = "";

    [Column(Name = "TypeID")] public int TypeId { get; set; } = 1;

    [Column(Name = "mark")] public string? Mark { get; set; }

    [Column(Name = "CreateTime")] public DateTime? CreateTime { get; set; }

    [Column(Name = "UpdateTime")] public DateTime? UpdateTime { get; set; }

    [Column(Name = "IsDeleted")] public bool? IsDeleted { get; set; }
}

/// <summary>
/// 产品类型（TestBed · Model 表，Type.TypeID 外键）
/// </summary>
[Table(Name = "Model")]
public class ProductCategoryEntity
{
    [Column(Name = "ID", IsPrimary = true)]
    public int Id { get; set; }

    [Column(Name = "ModelType")] public string ModelType { get; set; } = "";

    [Column(Name = "mark")] public string? Mark { get; set; }
}

/// <summary>
/// 离线模式产品型号（4 路回路预设，参数管理用 T_PRODUCT）
/// </summary>
[Table(Name = "T_PRODUCT")]
public class ProductEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(StringLength = 32, IsNullable = false)]
    public string Code { get; set; } = "";

    [Column(StringLength = 128, IsNullable = false)]
    public string Name { get; set; } = "";

    [Column(StringLength = 64)]
    public string? Category { get; set; }

    /// <summary>型号备注（界面「备注」列）</summary>
    [Column(StringLength = 256)]
    public string? Remark { get; set; }

    public double Loop1Voltage { get; set; }
    public double Loop2Voltage { get; set; }
    public double Loop3Voltage { get; set; }
    public double Loop4Voltage { get; set; }

    public int DurationSeconds { get; set; } = 60;
    public double ProtectCurrentMa { get; set; } = 500;

    /// <summary>回路 1～4 保护电流（mA）；为 0 时界面回退使用 <see cref="ProtectCurrentMa"/>。</summary>
    public double Loop1ProtectCurrentMa { get; set; }
    public double Loop2ProtectCurrentMa { get; set; }
    public double Loop3ProtectCurrentMa { get; set; }
    public double Loop4ProtectCurrentMa { get; set; }

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 数据设置 · 检查人员（TestBed · AuxiliaryTable：辅助人 / 互检 / 质检）
/// </summary>
[Table(Name = "AuxiliaryTable")]
public class InspectionStaffEntity
{
    [Column(Name = "ID", IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(Name = "auxiliariesCode1")] public string AssistJob1 { get; set; } = "";
    [Column(Name = "auxiliariesName1")] public string AssistName1 { get; set; } = "";
    [Column(Name = "auxiliariesCode2")] public string AssistJob2 { get; set; } = "";
    [Column(Name = "auxiliariesName2")] public string AssistName2 { get; set; } = "";
    [Column(Name = "auxiliariesCode3")] public string AssistJob3 { get; set; } = "";
    [Column(Name = "auxiliariesName3")] public string AssistName3 { get; set; } = "";
    [Column(Name = "auxiliariesCode4")] public string AssistJob4 { get; set; } = "";
    [Column(Name = "auxiliariesName4")] public string AssistName4 { get; set; } = "";
    [Column(Name = "mutualPerson")] public string MutualJob { get; set; } = "";
    [Column(Name = "mutualPersonName")] public string MutualName { get; set; } = "";
    [Column(Name = "qualityPerson")] public string QualityJob { get; set; } = "";
    [Column(Name = "qualityPersonName")] public string QualityName { get; set; } = "";
}

/// <summary>
/// 在线/Excel 导入模式：耐压任务（findHoldTaskList 返回的一条）
/// </summary>
[Table(Name = "T_HOLD_TASK")]
public class HoldTaskEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(StringLength = 32, IsNullable = false)]
    public string HoldTaskId { get; set; } = "";       // API holdTaskId

    [Column(StringLength = 32)]
    public string? HoldItemId { get; set; }            // API holdItemId

    [Column(StringLength = 32)]
    public string? ProjectType { get; set; }
    [Column(StringLength = 32)]
    public string? ProjectNumber { get; set; }
    [Column(StringLength = 32)]
    public string? TrainNo { get; set; }
    [Column(StringLength = 32)]
    public string? TrainCode { get; set; }
    [Column(StringLength = 32)]
    public string? CarCode { get; set; }
    [Column(StringLength = 32)]
    public string? DepId { get; set; }
    [Column(StringLength = 32)]
    public string? DebugType { get; set; }
    [Column(StringLength = 64)]
    public string? ItemName { get; set; }

    public TaskSource Source { get; set; }              // Online / Excel
    public bool IsCompleted { get; set; }
    public bool IsUploaded { get; set; }

    public DateTime DownloadedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public DateTime? UploadedAt { get; set; }
}

/// <summary>
/// 任务明细（findHoldTaskDetail 返回的一条 + 试验结果）
/// </summary>
[Table(Name = "T_HOLD_TASK_DETAIL")]
public class HoldTaskDetailEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    public int HoldTaskRowId { get; set; }              // 关联本地 HoldTaskEntity.Id

    // ===== API 工艺数据 =====
    [Column(StringLength = 32)]
    public string DetailId { get; set; } = "";          // API detailId
    [Column(StringLength = 64)]
    public string? StepName { get; set; }
    [Column(StringLength = 128)]
    public string? StepBom { get; set; }
    [Column(StringLength = 256)]
    public string? StepContent { get; set; }
    [Column(StringLength = 16)]
    public string? StepNo { get; set; }
    [Column(StringLength = 32)]
    public string? VhecileNo { get; set; }
    [Column(StringLength = 4)]
    public string? IsOperateCell { get; set; }
    [Column(StringLength = 64)]
    public string? ResultContent { get; set; }
    [Column(StringLength = 4)]
    public string? RecordType { get; set; }
    [Column(StringLength = 32)]
    public string? ResultStandard { get; set; }
    [Column(StringLength = 16)]
    public string? ResultUnit { get; set; }
    [Column(StringLength = 256)]
    public string? Remark { get; set; }

    // ===== 试验结果（对应 pullHoldTask 上传字段）=====
    public double TestTemperature { get; set; }
    public double TestHumidity { get; set; }
    public double ApplyVoltage { get; set; }            // 实际施加电压 V
    public double TestValue { get; set; }               // 结果值
    public int DurationSeconds { get; set; } = 60;
    public double ProtectCurrentMa { get; set; } = 500;

    /// <summary>过程数据，API 字符串格式："V,mA,time;V,mA,time;..."</summary>
    [Column(StringLength = -1)]
    public string? TestProcessValues { get; set; }

    public TestResult Result { get; set; }              // 自检结果

    [Column(StringLength = 32)]
    public string? OperatePerson { get; set; }
    [Column(StringLength = 64)]
    public string? OperatePersonName { get; set; }
    public DateTime? OperateTime { get; set; }
    public int OperateResult { get; set; }              // 0/1

    // 4 个辅助员
    [Column(StringLength = 32)] public string? AuxiliariesCode1 { get; set; }
    [Column(StringLength = 64)] public string? AuxiliariesName1 { get; set; }
    [Column(StringLength = 32)] public string? AuxiliariesCode2 { get; set; }
    [Column(StringLength = 64)] public string? AuxiliariesName2 { get; set; }
    [Column(StringLength = 32)] public string? AuxiliariesCode3 { get; set; }
    [Column(StringLength = 64)] public string? AuxiliariesName3 { get; set; }
    [Column(StringLength = 32)] public string? AuxiliariesCode4 { get; set; }
    [Column(StringLength = 64)] public string? AuxiliariesName4 { get; set; }

    // 互检
    [Column(StringLength = 32)] public string? MutualPerson { get; set; }
    [Column(StringLength = 64)] public string? MutualPersonName { get; set; }
    public DateTime? MutualTime { get; set; }
    public int MutualResult { get; set; }

    // 质检
    [Column(StringLength = 32)] public string? QualityPerson { get; set; }
    [Column(StringLength = 64)] public string? QualityPersonName { get; set; }
    public DateTime? QualityTime { get; set; }
    public int QualityResult { get; set; }

    public bool IsTested { get; set; }                  // 是否已做过试验
    public bool IsUploaded { get; set; }              // 明细是否已回传 MES
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

/// <summary>
/// 报表记录（保存报表时存储）
/// </summary>
[Table(Name = "Record")]
public class RecordEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(StringLength = 128)]
    public string? ProductModel { get; set; }

    [Column(StringLength = 128)]
    public string? VehicleNumber { get; set; }

    [Column(StringLength = 64)]
    public string? TesterName { get; set; }

    [Column(StringLength = 32)]
    public string? TestTime { get; set; }

    [Column(StringLength = 64)]
    public string? DeviceNumber { get; set; }

    [Column(StringLength = 256)]
    public string? Remarks { get; set; }

    [Column(StringLength = 32)]
    public string? LoopOneWithstandVoltage { get; set; }

    [Column(StringLength = 32)]
    public string? LoopOneLeakageCurrent { get; set; }

    [Column(StringLength = 16)]
    public string? LoopOneTestResult { get; set; }

    [Column(StringLength = 32)]
    public string? LoopTwoWithstandVoltage { get; set; }

    [Column(StringLength = 32)]
    public string? LoopTwoLeakageCurrent { get; set; }

    [Column(StringLength = 16)]
    public string? LoopTwoTestResult { get; set; }

    [Column(StringLength = 32)]
    public string? LoopThreeWithstandVoltage { get; set; }

    [Column(StringLength = 32)]
    public string? LoopThreeLeakageCurrent { get; set; }

    [Column(StringLength = 16)]
    public string? LoopThreeTestResult { get; set; }

    [Column(StringLength = 32)]
    public string? LoopFourWithstandVoltage { get; set; }

    [Column(StringLength = 32)]
    public string? LoopFourLeakageCurrent { get; set; }

    [Column(StringLength = 16)]
    public string? LoopFourTestResult { get; set; }

    [Column(StringLength = 32)]
    public string? InsulationResistance { get; set; }

    [Column(StringLength = 32)]
    public string? TurnWithstand { get; set; }

    [Column(StringLength = 16)]
    public string? TestResult { get; set; }

    [Column(StringLength = -1)]
    public string? ReportPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 离线模式独立试验记录（不走 API，存本地）
/// </summary>
[Table(Name = "T_OFFLINE_RECORD")]
public class OfflineRecordEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(StringLength = 32)]
    public string? ProductCode { get; set; }
    [Column(StringLength = 128)]
    public string? ProductName { get; set; }

    public int LoopIndex { get; set; }                  // 1~4
    public double TargetVoltage { get; set; }
    public double MaxVoltage { get; set; }
    public double MaxCurrent { get; set; }
    public int DurationSeconds { get; set; }
    public TestResult Result { get; set; }
    public double EnvTempC { get; set; }
    public double EnvHumidity { get; set; }

    [Column(StringLength = 32)]
    public string? OperatorId { get; set; }
    [Column(StringLength = 64)]
    public string? OperatorName { get; set; }

    [Column(StringLength = -1)]
    public string? TestProcessValues { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
}

/// <summary>
/// MES 4.3 作业数据待传队列：断网时冻结 payload，恢复后按时间顺序补传。
/// </summary>
[Table(Name = "T_MES_WORKDATA_PENDING")]
public class MesWorkDataPendingEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>本地 HoldTaskDetail.ID，成功后用于 MarkUploaded；无任务时为 0。</summary>
    public int LocalDetailId { get; set; }

    [Column(StringLength = 128)]
    public string TaskId { get; set; } = "";

    [Column(StringLength = 128)]
    public string PartCode { get; set; } = "";

    /// <summary>序列化后的 MesWorkDataUploadRequest JSON。</summary>
    [Column(StringLength = -1, IsNullable = false)]
    public string PayloadJson { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastAttemptAt { get; set; }
    public int AttemptCount { get; set; }

    [Column(StringLength = 512)]
    public string? LastError { get; set; }

    /// <summary>0=待传，1=已成功上传。</summary>
    public int Status { get; set; }
}

/// <summary>
/// 设备运行计数
/// </summary>
[Table(Name = "T_DEVICE_COUNTER")]
public class DeviceCounterEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column(StringLength = 64, IsNullable = false)]
    public string CounterKey { get; set; } = "";

    [Column(StringLength = 128)]
    public string? CounterName { get; set; }

    public long Value { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? LastResetAt { get; set; }
}
