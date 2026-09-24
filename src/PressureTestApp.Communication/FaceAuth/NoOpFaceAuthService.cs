using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Services;

namespace PressureTestApp.Communication.FaceAuth;

/// <summary>
/// 人脸识别占位实现 —— 当前不接入真实硬件，所有方法返回"不支持"。
/// 后续接入硬件后，新增 RealFaceAuthService 实现 IFaceAuthService 即可，
/// 在 DI 容器里替换即可生效。
/// </summary>
public class NoOpFaceAuthService : IFaceAuthService
{
    public bool IsAvailable => false;

    public Task<FaceAuthResult> AuthenticateAsync(CancellationToken ct = default)
    {
        NlogHelper.Info("[FaceAuth] 当前未接入硬件，返回 NotSupported");
        return Task.FromResult(FaceAuthResult.NotSupported());
    }

    public Task<bool> RegisterAsync(int userId, string userName)
    {
        NlogHelper.Info($"[FaceAuth] Register 调用被忽略（未接入）：user={userId}");
        return Task.FromResult(false);
    }

    public Task<bool> RemoveAsync(int userId)
    {
        return Task.FromResult(false);
    }
}
