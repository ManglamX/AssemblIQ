using System.Net.Http.Json;
using System.Text.Json;

Console.WriteLine("=================================================");
Console.WriteLine("  SmartTorque Hardware Simulator Console Tool");
Console.WriteLine("=================================================");

string baseUrl = "http://localhost:5084/api/readings";
string deviceId = "SIM-TORQUE-01";
string apiKey = "sim_torque_key_secret_12345";
decimal targetValue = 25.0m;
bool ramp = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--url" && i + 1 < args.Length) baseUrl = args[++i];
    else if (args[i] == "--device" && i + 1 < args.Length)
    {
        deviceId = args[++i];
        if (deviceId == "SIM-VERNIER-01") apiKey = "sim_vernier_key_secret_12345";
    }
    else if (args[i] == "--key" && i + 1 < args.Length) apiKey = args[++i];
    else if (args[i] == "--val" && i + 1 < args.Length && decimal.TryParse(args[++i], out var v)) targetValue = v;
    else if (args[i] == "--ramp") ramp = true;
}

Console.WriteLine($"Target Endpoint : {baseUrl}");
Console.WriteLine($"Transmitter ID  : {deviceId}");
Console.WriteLine($"Value           : {targetValue} {(deviceId == "SIM-TORQUE-01" ? "N·m" : "mm")}");
Console.WriteLine($"Mode            : {(ramp ? "Torque Ramp (0 -> Target)" : "Single Shot Reading")}");
Console.WriteLine("-------------------------------------------------");

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("X-Transmitter-Id", deviceId);
httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

long seq = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000;

if (ramp)
{
    int steps = 6;
    for (int step = 1; step <= steps; step++)
    {
        decimal currentVal = Math.Round((targetValue / steps) * step, 2);
        seq++;
        await PostReadingAsync(httpClient, baseUrl, deviceId, apiKey, seq, currentVal);
        await Task.Delay(350);
    }
}
else
{
    seq++;
    await PostReadingAsync(httpClient, baseUrl, deviceId, apiKey, seq, targetValue);
}

static async Task PostReadingAsync(HttpClient client, string url, string devId, string key, long seq, decimal val)
{
    var payload = new
    {
        TransmitterId = devId,
        ApiKey = key,
        DeviceSeq = seq,
        Value = val,
        CapturedAt = DateTime.UtcNow
    };

    try
    {
        Console.Write($"[{DateTime.Now:HH:mm:ss}] Transmitting seq={seq}, val={val}... ");
        var resp = await client.PostAsJsonAsync(url, payload);
        var json = await resp.Content.ReadAsStringAsync();

        if (resp.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            bool passed = root.TryGetProperty("isStepPassed", out var p) && p.GetBoolean();
            string label = root.TryGetProperty("stepLabel", out var l) ? l.GetString() ?? "" : "";
            int attempt = root.TryGetProperty("attemptNo", out var a) ? a.GetInt32() : 1;
            bool sessionDone = root.TryGetProperty("isSessionCompleted", out var sc) && sc.GetBoolean();

            Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"{(passed ? "PASS" : "FAIL")} — Step '{label}' (Attempt {attempt})");
            Console.ResetColor();

            if (sessionDone)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(">>> SESSION COMPLETED! All steps verified for this component. <<<");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"HTTP {(int)resp.StatusCode} - {json}");
            Console.ResetColor();
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"FAILED: {ex.Message}");
        Console.ResetColor();
    }
}