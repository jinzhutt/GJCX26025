using System.Text.Encodings.Web;
using System.Text.Json;
using RestSharp;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// MES 请求体序列化：中文按原文写出，避免默认 \uXXXX 转义。
/// </summary>
internal static class MesJson
{
    public static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 挂载 JSON 原文正文。
    /// 注意：不可给 BodyParameter 起名（如 "json"），RestSharp 会改成 multipart/form-data，
    /// MES 按 JSON 解析会报 JSON parse error。
    /// </summary>
    public static RestRequest AddMesJsonBody(this RestRequest request, string jsonBody)
        => request.AddStringBody(jsonBody, ContentType.Json);
}
