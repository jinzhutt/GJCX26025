using System.Text.Json;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Data.Entities;
using PressureTestApp.Data.Repositories;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// MES 4.3 断网保护：试验结束/上传失败时冻结 JSON 入队，网络恢复后按创建时间从旧到新补传。
/// </summary>
public sealed class MesPendingUploadService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly MesPendingUploadRepository _repo;
    private readonly LegacyTaskRepository _taskRepo;
    private readonly MesApiClient _mesApi;
    private readonly object _flushLock = new();
    private bool _flushing;

    public MesPendingUploadService(
        MesPendingUploadRepository repo,
        LegacyTaskRepository taskRepo,
        MesApiClient mesApi)
    {
        _repo = repo;
        _taskRepo = taskRepo;
        _mesApi = mesApi;
    }

    public Task<int> CountPendingAsync() => _repo.CountPendingAsync();

    /// <summary>冻结当前 4.3 请求体到本地队列（覆盖同一 LocalDetailId 的旧待传）。</summary>
    public async Task SavePendingAsync(MesWorkDataUploadRequest payload, int localDetailId = 0)
    {
        if (string.IsNullOrWhiteSpace(payload.TaskId) || string.IsNullOrWhiteSpace(payload.PartCode))
            throw new ArgumentException("taskId/partCode 为空，无法缓存 MES 待传数据");

        var entity = new MesWorkDataPendingEntity
        {
            LocalDetailId = localDetailId,
            TaskId = payload.TaskId.Trim(),
            PartCode = payload.PartCode.Trim(),
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            CreatedAt = DateTime.Now,
            Status = 0
        };

        var id = await _repo.UpsertPendingAsync(entity);
        NlogHelper.Info($"MES 待传已缓存：id={id}，taskId={entity.TaskId}，partCode={entity.PartCode}，localDetailId={localDetailId}");
    }

    public Task MarkUploadedByLocalDetailIdAsync(int localDetailId) =>
        _repo.MarkUploadedByLocalDetailIdAsync(localDetailId);

    /// <summary>
    /// 网络可达时按 CreatedAt 从旧到新补传；返回成功/失败/剩余条数。
    /// excludeLocalDetailIds：本次即将正式回传的明细，跳过其旧待传，避免同一任务发两次。
    /// </summary>
    public async Task<(int Ok, int Fail, int Remaining)> FlushPendingAsync(
        IReadOnlyCollection<int>? excludeLocalDetailIds = null,
        CancellationToken cancellationToken = default)
    {
        lock (_flushLock)
        {
            if (_flushing)
                return (0, 0, -1);
            _flushing = true;
        }

        try
        {
            var (reachable, error) = await _mesApi.CanReachAsync(cancellationToken);
            if (!reachable)
            {
                NlogHelper.Info($"MES 待传补传跳过：不可达（{error}）");
                var remOffline = await _repo.CountPendingAsync();
                return (0, 0, remOffline);
            }

            var list = await _repo.ListPendingOldestFirstAsync();
            int ok = 0, fail = 0;
            var exclude = excludeLocalDetailIds is { Count: > 0 }
                ? excludeLocalDetailIds.Where(id => id > 0).ToHashSet()
                : null;

            foreach (var row in list)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (exclude != null && row.LocalDetailId > 0 && exclude.Contains(row.LocalDetailId))
                {
                    NlogHelper.Info($"MES 待传补传跳过（本次将正式回传）：id={row.Id}，localDetailId={row.LocalDetailId}");
                    continue;
                }

                try
                {
                    var payload = JsonSerializer.Deserialize<MesWorkDataUploadRequest>(row.PayloadJson, JsonOptions);
                    if (payload == null || string.IsNullOrWhiteSpace(payload.TaskId))
                    {
                        await _repo.MarkAttemptFailedAsync(row.Id, "PayloadJson 无效");
                        fail++;
                        continue;
                    }

                    var resp = await _mesApi.UploadWorkDataAsync(payload, cancellationToken);
                    if (!resp.IsOk)
                    {
                        await _repo.MarkAttemptFailedAsync(row.Id, $"code={resp.Code} {resp.StatusText}");
                        fail++;
                        NlogHelper.Warn($"MES 待传补传失败：id={row.Id}，taskId={row.TaskId}，msg={resp.StatusText}");
                        // 网络/服务异常时后面的也多半失败，先停，避免刷屏
                        break;
                    }

                    await _repo.MarkUploadedAsync(row.Id);
                    if (row.LocalDetailId > 0)
                    {
                        try { await _taskRepo.MarkUploadedAsync(row.LocalDetailId); }
                        catch (Exception exMark)
                        {
                            NlogHelper.Warn(exMark, $"待传成功但 MarkUploaded 失败：localDetailId={row.LocalDetailId}");
                        }
                    }

                    ok++;
                    NlogHelper.Info($"MES 待传补传成功：id={row.Id}，taskId={row.TaskId}");
                }
                catch (Exception ex)
                {
                    await _repo.MarkAttemptFailedAsync(row.Id, ex.Message);
                    fail++;
                    NlogHelper.Warn(ex, $"MES 待传补传异常：id={row.Id}");
                    break;
                }
            }

            var remaining = await _repo.CountPendingAsync();
            return (ok, fail, remaining);
        }
        finally
        {
            lock (_flushLock) _flushing = false;
        }
    }
}
