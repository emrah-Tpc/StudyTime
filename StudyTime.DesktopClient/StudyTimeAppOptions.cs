using System.Text.Json;

namespace StudyTime.DesktopClient;

/// <summary>
/// appsettings.json + ortam değişkenlerinden okunan istemci yapılandırması (tek örnek, DI ile verilir).
/// </summary>
public sealed class StudyTimeAppOptions
{
    public const string ApiBaseUrlEnvVar = "STUDYTIME_API_BASE_URL";
    public const string LocalOnlyEnvVar = "STUDYTIME_LOCAL_ONLY";

    public string ApiBaseUrl { get; private init; } = "";
    public bool LocalOnlyMode { get; private init; }

    public static StudyTimeAppOptions Load()
    {
        var (url, localFromFile) = TryLoadFromFiles();
        var localOnly = ParseLocalOnlyEnv() ?? localFromFile;
        if (MauiProgram.IsOfflineBeta)
            localOnly = true;

        var apiUrl = NormalizeApiBaseUrl(ParseApiBaseUrlEnv() ?? url);

        return new StudyTimeAppOptions
        {
            ApiBaseUrl = apiUrl,
            LocalOnlyMode = localOnly
        };
    }

    private static bool? ParseLocalOnlyEnv()
    {
        var v = Environment.GetEnvironmentVariable(LocalOnlyEnvVar);
        if (string.IsNullOrWhiteSpace(v))
            return null;
        if (string.Equals(v, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(v, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "no", StringComparison.OrdinalIgnoreCase))
            return false;
        return null;
    }

    private static string? ParseApiBaseUrlEnv()
    {
        var fromEnv = Environment.GetEnvironmentVariable(ApiBaseUrlEnvVar);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv;
    }

    private static (string? ApiUrl, bool LocalOnly) TryLoadFromFiles()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
                return ParseStudyTimeSection(File.ReadAllText(path));
        }
        catch
        {
            // yoksay
        }

        try
        {
            var asm = typeof(StudyTimeAppOptions).Assembly;
            using var stream = asm.GetManifestResourceStream("StudyTime.DesktopClient.appsettings.json");
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                return ParseStudyTimeSection(reader.ReadToEnd());
            }
        }
        catch
        {
            // yoksay
        }

        return (null, false);
    }

    private static (string? ApiUrl, bool LocalOnly) ParseStudyTimeSection(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("StudyTime", out var st))
                return (null, false);

            string? url = null;
            if (st.TryGetProperty("ApiBaseUrl", out var urlEl))
                url = urlEl.GetString();

            var localOnly = false;
            if (st.TryGetProperty("LocalOnlyMode", out var locEl))
            {
                if (locEl.ValueKind == JsonValueKind.True)
                    localOnly = true;
                else if (locEl.ValueKind == JsonValueKind.String)
                {
                    var s = locEl.GetString();
                    localOnly = string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
                }
            }

            return (url, localOnly);
        }
        catch
        {
            return (null, false);
        }
    }

    private static string NormalizeApiBaseUrl(string? url)
    {
        var isAndroid = Microsoft.Maui.Devices.DeviceInfo.Platform
                        == Microsoft.Maui.Devices.DevicePlatform.Android;

        if (string.IsNullOrWhiteSpace(url))
        {
            return isAndroid
                ? "https://gjdz7mbz-7288.euw.devtunnels.ms/"
                : "https://localhost:7288/";
        }

        var t = url.Trim();
        if (!t.EndsWith('/')) t += "/";

        if (isAndroid && t.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            // Android'de ayar dosyalarından (appsettings) gelen URL localhost ise, 
            // direkt olarak Dev Tunnels adresine yönlendirilir.
            return "https://gjdz7mbz-7288.euw.devtunnels.ms/";
        }

        return t;
    }
}
