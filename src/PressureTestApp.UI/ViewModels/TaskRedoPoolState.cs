using System.Collections.Generic;

namespace PressureTestApp.UI.ViewModels;

/// <summary>
/// 任务重做后：该行从「任务回传」数据源移除，并出现在「任务查看」数据源中（跨多次打开窗口共享，直至进程结束）。
/// </summary>
public static class TaskRedoPoolState
{
    private static readonly HashSet<string> DetailIdsInTaskViewPool = new();
    private static readonly object Gate = new();

    public static void TransferToTaskView(string detailId)
    {
        if (string.IsNullOrWhiteSpace(detailId))
            return;
        lock (Gate)
            DetailIdsInTaskViewPool.Add(detailId);
    }

    public static bool IsInTaskViewPool(string detailId)
    {
        if (string.IsNullOrWhiteSpace(detailId))
            return false;
        lock (Gate)
            return DetailIdsInTaskViewPool.Contains(detailId);
    }
}
