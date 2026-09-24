using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Urls.Clear();
app.Urls.Add("http://127.0.0.1:5088");

app.MapGet("/bri/biz/integration/getWorkpieceInfo", (string? stationCode, string? deviceId) =>
{
    Console.WriteLine($"[4.1] stationCode={stationCode}, deviceId={deviceId}");
    return Results.Json(new
    {
        success = true,
        message = "本机模拟：获取作业工件信息成功",
        code = 200,
        result = new[]
        {
            new
            {
                trainType = "HXD1",
                trainNo = "7282",
                partCode = "ZC-PART-0001",
                partType = "轴承退卸",
                taskId = "MOCK-BEARING-TASK-001"
            },
            new
            {
                trainType = "HXD3C",
                trainNo = "024",
                partCode = "ZC-PART-0002",
                partType = "轴承退卸",
                taskId = "MOCK-BEARING-TASK-002"
            }
        },
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    });
});

app.MapGet("/bri/biz/integration/getPersonInfoByFaceScan", (string? stationCode) =>
{
    Console.WriteLine($"[4.2] stationCode={stationCode}");
    return Results.Json(new
    {
        success = true,
        message = "本机模拟：获取刷脸人员成功",
        code = 200,
        result = new { userName = "张三", userCode = "A10123" },
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    });
});

app.MapPost("/bri/biz/integration/uploadWorkData", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine($"[4.3] body length={body.Length}");
    try
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var root = doc.RootElement;
        if (root.TryGetProperty("taskId", out var taskId))
            Console.WriteLine($"[4.3] taskId={taskId}");
        if (root.TryGetProperty("partCode", out var partCode))
            Console.WriteLine($"[4.3] partCode={partCode}");
        if (root.TryGetProperty("worker", out var worker))
            Console.WriteLine($"[4.3] worker={worker}");
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            var hasCurve = data.TryGetProperty("位移压力变化过程曲线", out var curve)
                && curve.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(curve.GetString());
            Console.WriteLine($"[4.3] 位移压力变化过程曲线={(hasCurve ? "有图" : "无图")}");
        }
    }
    catch
    {
        // ignore
    }

    return Results.Json(new
    {
        success = true,
        message = "本机模拟：设备作业数据回传成功",
        code = 200,
        result = (object?)null,
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    });
});

app.MapPost("/bri/biz/integration/uploadDeviceStatus", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine($"[4.4] {body}");
    return Results.Json(new
    {
        success = true,
        message = "本机模拟：设备状态上报成功",
        code = 200,
        result = (object?)null,
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    });
});

app.MapPost("/bri/biz/integration/uploadDeviceFault", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine($"[4.5] {body}");
    return Results.Json(new
    {
        success = true,
        message = "本机模拟：设备故障上报成功",
        code = 200,
        result = (object?)null,
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    });
});

Console.WriteLine("Mock MES（轴承退卸）已启动：http://127.0.0.1:5088/bri/biz");
Console.WriteLine("按 Ctrl+C 停止。");
app.Run();
