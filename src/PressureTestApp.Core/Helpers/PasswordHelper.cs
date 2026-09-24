using System.Security.Cryptography;
using System.Text;

namespace PressureTestApp.Core.Helpers;

/// <summary>
/// 密码哈希工具（工号作盐 + SHA512 迭代 1024 次 + Base64）
/// </summary>
public static class PasswordHelper
{
    /// <summary>
    /// SHA512 加密；salt 为工号，password 为明文密码
    /// </summary>
    public static string SHA512Encode(string salt, string password)
    {
        var saltPasswordValue = Encoding.UTF8.GetBytes(salt + password);
        saltPasswordValue = SHA512.HashData(saltPasswordValue);
        for (var i = 0; i < 1023; i++)
            saltPasswordValue = SHA512.HashData(saltPasswordValue);
        return Convert.ToBase64String(saltPasswordValue);
    }
}
