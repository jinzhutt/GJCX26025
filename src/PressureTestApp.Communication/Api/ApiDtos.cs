using System.Text.Json.Serialization;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// EPE API 通用响应外壳
/// </summary>
public class ApiResponse
{
    [JsonPropertyName("state")]  public string? State { get; set; }
    [JsonPropertyName("msg")]    public string? Msg { get; set; }
    [JsonPropertyName("total")]  public string? Total { get; set; }

    public bool IsSuccess => State == "1";
}

public class ApiResponse<T> : ApiResponse
{
    [JsonPropertyName("list")] public List<T>? List { get; set; }
}

// ===== 3.1 设备授权 =====
public class DeviceAuthorizeResponse : ApiResponse
{
    [JsonPropertyName("deviceNumber")] public string? DeviceNumber { get; set; }
}

// ===== 3.3 人员同步 =====
public class PersonDto
{
    [JsonPropertyName("personCode")] public string? PersonCode { get; set; }
    [JsonPropertyName("personName")] public string? PersonName { get; set; }
    [JsonPropertyName("password")]   public string? Password { get; set; }    // MD5
    [JsonPropertyName("depId")]      public string? DepId { get; set; }
    [JsonPropertyName("depName")]    public string? DepName { get; set; }
}

// ===== 3.4 修改密码 =====
public class ModifyPasswordResponse : ApiResponse
{
    [JsonPropertyName("newPwd")] public string? NewPwd { get; set; }
}

// ===== 3.5 车型 =====
public class ProjectDto
{
    [JsonPropertyName("projectId")]        public int ProjectId { get; set; }
    [JsonPropertyName("projectType")]      public string? ProjectType { get; set; }
    [JsonPropertyName("projectNumber")]    public string? ProjectNumber { get; set; }
    [JsonPropertyName("projectName")]      public string? ProjectName { get; set; }
    [JsonPropertyName("wbsCode")]          public string? WbsCode { get; set; }
    [JsonPropertyName("projectNumberAll")] public string? ProjectNumberAll { get; set; }
    [JsonPropertyName("vehicleName")]      public string? VehicleName { get; set; }
}

// ===== 3.6 车列 =====
public class TrainDto
{
    [JsonPropertyName("id")]        public int Id { get; set; }
    [JsonPropertyName("trainNo")]   public string? TrainNo { get; set; }
    [JsonPropertyName("trainCode")] public string? TrainCode { get; set; }
    [JsonPropertyName("projectId")] public string? ProjectId { get; set; }
}

// ===== 3.7 任务列表 =====
public class HoldTaskDto
{
    [JsonPropertyName("holdTaskId")]      public string? HoldTaskId { get; set; }
    [JsonPropertyName("projectTypeName")] public string? ProjectTypeName { get; set; }
    [JsonPropertyName("projectNumber")]   public string? ProjectNumber { get; set; }
    [JsonPropertyName("trainCode")]       public string? TrainCode { get; set; }
    [JsonPropertyName("trainNo")]         public string? TrainNo { get; set; }
    [JsonPropertyName("carCode")]         public string? CarCode { get; set; }
    [JsonPropertyName("debugType")]       public string? DebugType { get; set; }
    [JsonPropertyName("depId")]           public string? DepId { get; set; }
    [JsonPropertyName("holdItems")]       public List<HoldItemDto>? HoldItems { get; set; }
}

public class HoldItemDto
{
    [JsonPropertyName("holdItemId")]     public string? HoldItemId { get; set; }
    [JsonPropertyName("itemName")]       public string? ItemName { get; set; }
    [JsonPropertyName("operateProcess")] public string? OperateProcess { get; set; }
    [JsonPropertyName("mutualProcess")]  public string? MutualProcess { get; set; }
    [JsonPropertyName("qualityProcess")] public string? QualityProcess { get; set; }
}

// ===== 3.8 任务明细 =====
public class HoldTaskDetailDto
{
    [JsonPropertyName("holdTaskId")]     public string? HoldTaskId { get; set; }
    [JsonPropertyName("holdItemId")]     public string? HoldItemId { get; set; }
    [JsonPropertyName("detailId")]       public string? DetailId { get; set; }
    [JsonPropertyName("stepName")]       public string? StepName { get; set; }
    [JsonPropertyName("stepBom")]        public string? StepBom { get; set; }
    [JsonPropertyName("stepContent")]    public string? StepContent { get; set; }
    [JsonPropertyName("stepNo")]         public string? StepNo { get; set; }
    [JsonPropertyName("vhecileNo")]      public string? VhecileNo { get; set; }
    [JsonPropertyName("isOperateCell")]  public string? IsOperateCell { get; set; }
    [JsonPropertyName("resultContent")]  public string? ResultContent { get; set; }
    [JsonPropertyName("recordType")]     public string? RecordType { get; set; }
    [JsonPropertyName("resultStandard")] public string? ResultStandard { get; set; }
    [JsonPropertyName("resultUnit")]     public string? ResultUnit { get; set; }
    [JsonPropertyName("remark")]         public string? Remark { get; set; }
}

// ===== 3.9 结果回传 =====
public class UploadResultRequest
{
    [JsonPropertyName("deviceNumber")] public string? DeviceNumber { get; set; }
    [JsonPropertyName("holdTaskId")]   public string? HoldTaskId { get; set; }
    [JsonPropertyName("holdItemId")]   public string? HoldItemId { get; set; }
    [JsonPropertyName("list")]         public List<UploadResultDetail>? List { get; set; }
}

public class UploadResultDetail
{
    [JsonPropertyName("detailId")]          public string? DetailId { get; set; }
    [JsonPropertyName("testTemperature")]   public string? TestTemperature { get; set; }
    [JsonPropertyName("testHumidity")]      public string? TestHumidity { get; set; }
    [JsonPropertyName("applyVoltage")]      public string? ApplyVoltage { get; set; }
    [JsonPropertyName("testValue")]         public string? TestValue { get; set; }
    [JsonPropertyName("testProcessValues")] public string? TestProcessValues { get; set; }
    [JsonPropertyName("remark")]            public string? Remark { get; set; }

    [JsonPropertyName("operatePersonName")] public string? OperatePersonName { get; set; }
    [JsonPropertyName("operatePerson")]     public string? OperatePerson { get; set; }
    [JsonPropertyName("operateTime")]       public string? OperateTime { get; set; }
    [JsonPropertyName("operateResult")]     public string? OperateResult { get; set; }

    [JsonPropertyName("auxiliariesName1")]  public string? AuxiliariesName1 { get; set; }
    [JsonPropertyName("auxiliariesCode1")]  public string? AuxiliariesCode1 { get; set; }
    [JsonPropertyName("auxiliariesName2")]  public string? AuxiliariesName2 { get; set; }
    [JsonPropertyName("auxiliariesCode2")]  public string? AuxiliariesCode2 { get; set; }
    [JsonPropertyName("auxiliariesName3")]  public string? AuxiliariesName3 { get; set; }
    [JsonPropertyName("auxiliariesCode3")]  public string? AuxiliariesCode3 { get; set; }
    [JsonPropertyName("auxiliariesName4")]  public string? AuxiliariesName4 { get; set; }
    [JsonPropertyName("auxiliariesCode4")]  public string? AuxiliariesCode4 { get; set; }

    [JsonPropertyName("mutualPersonName")]  public string? MutualPersonName { get; set; }
    [JsonPropertyName("mutualPerson")]      public string? MutualPerson { get; set; }
    [JsonPropertyName("mutualTime")]        public string? MutualTime { get; set; }
    [JsonPropertyName("mutualResult")]      public string? MutualResult { get; set; }

    [JsonPropertyName("qualityPersonName")] public string? QualityPersonName { get; set; }
    [JsonPropertyName("qualityPerson")]     public string? QualityPerson { get; set; }
    [JsonPropertyName("qualityTime")]       public string? QualityTime { get; set; }
    [JsonPropertyName("qualityResult")]     public string? QualityResult { get; set; }
}
