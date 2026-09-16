using System.Text.Json;

namespace CbtKiosk;

public enum QuitPasswordResult
{
    Correct,
    Wrong,
    Expired,
    Unavailable,
    Unreachable,
}

/// <summary>
/// Fetches the current quit/unlock password from the CBT kiosk endpoint(s) and validates a
/// candidate against it. Falls back to a locally configured offline password when no endpoint
/// can be reached.
/// </summary>
public sealed class KioskClient
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CbtKiosk/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        return client;
    }

    private readonly AppSettings _settings;
    private readonly Random _random = new();

    public KioskClient(AppSettings settings) => _settings = settings;

    public sealed record KioskPassword(string Password, DateTime? ExpiresAt, bool IsExpired, string Endpoint);

    /// <summary>Validates <paramref name="candidate"/> and returns why it was accepted or rejected.</summary>
    public QuitPasswordResult Validate(string candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return QuitPasswordResult.Wrong;

        var endpoints = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.KioskUrl)) endpoints.Add(_settings.KioskUrl);
        if (_settings.HasFallbackUrl && !endpoints.Contains(_settings.FallbackUrl)) endpoints.Add(_settings.FallbackUrl);

        var anyReachable = false;

        foreach (var endpoint in endpoints)
        {
            if (TryFetch(endpoint, out var kp))
            {
                anyReachable = true;
                Logger.Info($"Endpoint '{endpoint}' reachable; is_expired={kp.IsExpired} expires={kp.ExpiresAt:o}");

                if (kp.IsExpired || (kp.ExpiresAt.HasValue && kp.ExpiresAt.Value < DateTime.Now))
                {
                    Logger.Warn("Endpoint reports an expired password.");
                    return QuitPasswordResult.Expired;
                }

                if (string.IsNullOrEmpty(kp.Password))
                {
                    Logger.Warn("Endpoint returned an empty password.");
                    return QuitPasswordResult.Unavailable;
                }

                return Hash.FixedTimeEquals(candidate, kp.Password)
                    ? QuitPasswordResult.Correct
                    : QuitPasswordResult.Wrong;
            }
        }

        if (anyReachable)
        {
            // The server answered but we could not use it.
            return QuitPasswordResult.Unavailable;
        }

        // Nothing reachable -> offline fallback.
        Logger.Warn("No CBT endpoint reachable - trying offline fallback password.");
        if (_settings.HasOfflineFallback)
        {
            var candidateHash = Hash.Sha256Hex(candidate);
            return Hash.FixedTimeEquals(candidateHash, _settings.OfflineFallbackPasswordHash.ToLowerInvariant())
                ? QuitPasswordResult.Correct
                : QuitPasswordResult.Wrong;
        }

        return QuitPasswordResult.Unreachable;
    }

    /// <summary>Fetches and parses the kiosk password from a single endpoint, retrying as configured.</summary>
    public bool TryFetch(string url, out KioskPassword password)
    {
        password = null;
        if (string.IsNullOrWhiteSpace(url)) return false;

        for (var attempt = 1; attempt <= Math.Max(1, _settings.KioskAttempts); attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(500, _settings.KioskTimeoutMs)));
                using var response = Http.GetAsync(url, cts.Token).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"Attempt {attempt}/{_settings.KioskAttempts} to '{url}' -> HTTP {(int)response.StatusCode}");
                }
                else if (TryParse(body, out var parsed))
                {
                    password = parsed with { Endpoint = url };
                    return true;
                }
                else
                {
                    Logger.Warn($"Attempt {attempt}/{_settings.KioskAttempts} to '{url}' -> unparseable response: {Truncate(body)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Attempt {attempt}/{_settings.KioskAttempts} to '{url}' failed: {ex.Message}");
            }

            if (attempt < Math.Max(1, _settings.KioskAttempts))
            {
                var delay = Math.Max(0, _settings.KioskAttemptIntervalMs);
                if (delay > 0) Thread.Sleep(delay + _random.Next(0, 250)); // small jitter
            }
        }

        return false;
    }

    private static bool TryParse(string body, out KioskPassword password)
    {
        password = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // {"success":true,"data":{...}}
            var success = true;
            if (root.TryGetProperty("success", out var s) &&
                (s.ValueKind == JsonValueKind.False))
            {
                success = false;
            }
            if (!success) return false;

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return false;

            string pwd = null;
            if (data.TryGetProperty("exit_password", out var ep) && ep.ValueKind == JsonValueKind.String)
                pwd = ep.GetString();

            bool isExpired = false;
            if (data.TryGetProperty("is_expired", out var ie) &&
                (ie.ValueKind == JsonValueKind.True || ie.ValueKind == JsonValueKind.False))
                isExpired = ie.GetBoolean();

            DateTime? expiresAt = null;
            if (data.TryGetProperty("password_expires_at", out var pe) && pe.ValueKind == JsonValueKind.String)
            {
                var raw = pe.GetString();
                if (!string.IsNullOrWhiteSpace(raw) &&
                    DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dt))
                {
                    expiresAt = dt;
                }
            }

            password = new KioskPassword(pwd ?? string.Empty, expiresAt, isExpired, "");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("JSON parse error: " + ex.Message);
            return false;
        }
    }

    private static string Truncate(string value)
        => string.IsNullOrEmpty(value) ? "(empty)" : (value.Length <= 200 ? value : value[..200] + "...");

    /// <summary>Quick connectivity probe used by the settings window.</summary>
    public bool TestConnection(string url, out string message)
    {
        message = "";
        if (TryFetch(url, out var kp))
        {
            message = kp.IsExpired
                ? $"OK - endpoint terjangkau, TETAPI password dilaporkan KEDALUWARSA (expires: {kp.ExpiresAt:yyyy-MM-dd HH:mm})."
                : $"OK - endpoint terjangkau. Password tersedia, kedaluwarsa: {(kp.ExpiresAt?.ToString("yyyy-MM-dd HH:mm") ?? "(tidak disebutkan)")}.";
            return true;
        }
        message = "GAGAL - endpoint tidak dapat dihubungi atau respons tidak valid. Lihat log untuk detail.";
        return false;
    }
}
