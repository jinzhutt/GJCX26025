using FreeSql.DataAnnotations;

namespace PressureTestApp.Data.Entities;

/// <summary>��һ��ʽ��ѹ�� MainUI һ�µ���������������TestBed.db �� MainTaskTable����</summary>
[Table(Name = "MainTaskTable")]
public class MainTaskTableEntity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public int ID { get; set; }

    public string holdTaskId { get; set; } = "";
    public string? projectTypeName { get; set; }
    public string? projectNumber { get; set; }
    public string? trainNo { get; set; }
    public string? trainCode { get; set; }
    public string? carCode { get; set; }
    public string? depId { get; set; }
    public string? debugType { get; set; }
    public string? holdItemId { get; set; }
    public string? itemName { get; set; }
    public string? operateProcess { get; set; }
    public string? mutualProcess { get; set; }
    public string? qualityProcess { get; set; }
}

/// <summary>��һ��ʽ��ѹ�� MainUI һ�µ���ѹ������ϸ����TestBed.db �� HoldTaskTable����</summary>
[Table(Name = "HoldTaskTable")]
public class HoldTaskTableEntity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public int ID { get; set; }

    public string? stepId { get; set; }
    public string holdTaskId { get; set; } = "";
    public string? holdItemId { get; set; }
    public string detailId { get; set; } = "";
    public string? stepName { get; set; }
    public string? stepBom { get; set; }
    public string? stepContent { get; set; }
    public string? stepNo { get; set; }
    public string? vhecileNo { get; set; }
    public string? isOperateCell { get; set; }
    public string? resultContent { get; set; }
    public string? recordType { get; set; }
    public string? resultStandard { get; set; }
    public string? resultUnit { get; set; }
    public string? remark { get; set; }
    public string? testTemperature { get; set; }
    public string? testHumidity { get; set; }
    public string? applyVoltage { get; set; }
    public string? testValue { get; set; }
    public string? testProcessValues { get; set; }
    public string? operatePersonName { get; set; }
    public string? operatePerson { get; set; }
    public string? operateTime { get; set; }
    public string? operateResult { get; set; }
    public string? auxiliariesName1 { get; set; }
    public string? auxiliariesCode1 { get; set; }
    public string? auxiliariesName2 { get; set; }
    public string? auxiliariesCode2 { get; set; }
    public string? auxiliariesName3 { get; set; }
    public string? auxiliariesCode3 { get; set; }
    public string? auxiliariesName4 { get; set; }
    public string? auxiliariesCode4 { get; set; }
    public string? mutualPersonName { get; set; }
    public string? mutualPerson { get; set; }
    public string? mutualTime { get; set; }
    public string? mutualResult { get; set; }
    public string? qualityPersonName { get; set; }
    public string? qualityPerson { get; set; }
    public string? qualityTime { get; set; }
    public string? qualityResult { get; set; }

    /// <summary>0 �����أ�1 ������ɴ��ϴ���2 ���ϴ�</summary>
    public int isComplete { get; set; }

    public string? CreateTime { get; set; }
}
