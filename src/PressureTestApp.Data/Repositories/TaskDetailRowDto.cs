using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

/// <summary>MainTaskTable + HoldTaskTable ������ѯ�����</summary>
public sealed class TaskDetailRowDto
{
    public int LocalDetailId { get; init; }
    public int LocalTaskRowId { get; init; }
    public MainTaskTableEntity Main { get; init; } = null!;
    public HoldTaskTableEntity Detail { get; init; } = null!;
}
