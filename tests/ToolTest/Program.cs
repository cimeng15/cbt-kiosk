using System;
using System.IO;
using CbtKioskTool;

var failures = 0;
void Check(string name, bool condition, string detail = "")
{
    Console.WriteLine($"  [{(condition ? "PASS" : "FAIL")}] {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
    if (!condition) failures++;
}

// Redirect the per-user settings folder into a scratch directory so the test never touches the
// real configuration - both the tool and the kiosk resolve their paths from this variable.
var scratch = Path.Combine(Path.GetTempPath(), "cbt-tool-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", scratch);
Environment.SetEnvironmentVariable("APPDATA", scratch);          // for Windows runs of this test
Environment.SetEnvironmentVariable("ProgramData", scratch);

Console.WriteLine("=== 1. JSON sendiri (tanpa pustaka) ===");
var sample = "{\"success\":true,\"data\":{\"exit_password\":\"Nes\\\"cafe\",\"password_expires_at\":\"2026-09-28T23:55\",\"is_expired\":false,\"n\":-12.5,\"list\":[1,2,3],\"nested\":{\"a\":null}}}";
var root = Json.AsObject(Json.Parse(sample));
Check("objek tingkat atas terparse", root != null);
var data = root != null && root.ContainsKey("data") ? Json.AsObject(root["data"]) : null;
Check("objek bersarang terparse", data != null);
Check("string dengan escape kutip ganda", data != null && Json.AsString(data["exit_password"]) == "Nes\"cafe",
    data != null ? Json.AsString(data["exit_password"]) : "(null)");
Check("boolean false terbaca benar", data != null && Json.AsBool(data["is_expired"], true) == false);
Check("angka pecahan/negatif", data != null && Math.Abs(Convert.ToDouble(data["n"]) + 12.5) < 0.0001);
Check("array terbaca", data != null && data.ContainsKey("list"));
Check("null terbaca sebagai null", data != null && data.ContainsKey("nested"));

var broken = new[] { "{\"a\":}", "{\"a\" 1}", "{\"a\":1", "[1,2", "{\"a\":\"x}" };
var allThrew = true;
foreach (var text in broken)
{
    try { Json.Parse(text); allThrew = false; Console.WriteLine("    (tidak melempar: " + text + ")"); }
    catch (FormatException) { /* expected */ }
}
Check("JSON rusak ditolak (melempar FormatException)", allThrew);

Console.WriteLine();
Console.WriteLine("=== 2. Tulis lalu baca kembali (round-trip) ===");
var tool = new CbtKioskTool.AppSettings
{
    BaseUrl = "https://cbt.example.sch.id",
    StartUrl = "https://cbt.example.sch.id/ujian?x=1&y=\"2\"",
    KioskUrl = "https://cbt.example.sch.id/api/kiosk/settings",
    FallbackUrl = "https://cadangan.example.sch.id/api/kiosk/settings",
    KioskTimeoutMs = 7500,
    KioskAttempts = 4,
    KioskAttemptIntervalMs = 250,
    OfflineFallbackPasswordHash = CbtKioskTool.Hash.Sha256Hex("rahasia-darurat"),
    AllowDefaultEmergencyPassword = false,
    BlockNavigationKeys = false,
    AllowZoom = true,
    AllowBackNavigation = true,
    SettingsPasswordHash = CbtKioskTool.Hash.Sha256Hex("pengaturan-2026"),
    ClearSessionOnQuit = false,
    AutoStart = true,
    AutoStartAllUsers = true,
    StartupMethod = CbtKioskTool.Kiosk.MethodTask,
};

var json = Json.SerializeSettings(tool);
var back = CbtKioskTool.AppSettings.FromJson(Json.AsObject(Json.Parse(json)));
Check("BaseUrl", back.BaseUrl == tool.BaseUrl);
Check("StartUrl dengan karakter kutip & ampersand", back.StartUrl == tool.StartUrl, back.StartUrl);
Check("FallbackUrl", back.FallbackUrl == tool.FallbackUrl);
Check("KioskTimeoutMs", back.KioskTimeoutMs == tool.KioskTimeoutMs);
Check("KioskAttempts", back.KioskAttempts == tool.KioskAttempts);
Check("KioskAttemptIntervalMs", back.KioskAttemptIntervalMs == tool.KioskAttemptIntervalMs);
Check("OfflineFallbackPasswordHash", back.OfflineFallbackPasswordHash == tool.OfflineFallbackPasswordHash);
Check("AllowDefaultEmergencyPassword", back.AllowDefaultEmergencyPassword == tool.AllowDefaultEmergencyPassword);
Check("BlockNavigationKeys", back.BlockNavigationKeys == tool.BlockNavigationKeys);
Check("AllowZoom", back.AllowZoom == tool.AllowZoom);
Check("AllowBackNavigation", back.AllowBackNavigation == tool.AllowBackNavigation);
Check("SettingsPasswordHash", back.SettingsPasswordHash == tool.SettingsPasswordHash);
Check("ClearSessionOnQuit", back.ClearSessionOnQuit == tool.ClearSessionOnQuit);
Check("AutoStart", back.AutoStart == tool.AutoStart);
Check("AutoStartAllUsers", back.AutoStartAllUsers == tool.AutoStartAllUsers);
Check("StartupMethod", back.StartupMethod == tool.StartupMethod, back.StartupMethod);
Check("semua nama properti yang dibaca kiosk ikut tertulis",
    json.Contains("\"BaseUrl\"") && json.Contains("\"StartUrl\"") && json.Contains("\"KioskUrl\"") &&
    json.Contains("\"FallbackUrl\"") && json.Contains("\"KioskTimeoutMs\"") && json.Contains("\"KioskAttempts\"") &&
    json.Contains("\"KioskAttemptIntervalMs\"") && json.Contains("\"OfflineFallbackPasswordHash\"") &&
    json.Contains("\"AllowDefaultEmergencyPassword\"") && json.Contains("\"BlockNavigationKeys\"") &&
    json.Contains("\"AllowZoom\"") && json.Contains("\"AllowBackNavigation\"") &&
    json.Contains("\"SettingsPasswordHash\"") && json.Contains("\"ClearSessionOnQuit\"") &&
    json.Contains("\"AutoStart\"") && json.Contains("\"AutoStartAllUsers\"") && json.Contains("\"StartupMethod\""));

Console.WriteLine();
Console.WriteLine("=== 3. Kiosk asli membaca berkas yang ditulis tool ===");
string savedPath;
try
{
    savedPath = SettingsStore.Save(tool);
    Check("berkas tersimpan", File.Exists(savedPath), savedPath);
}
catch (Exception ex)
{
    Check("berkas tersimpan", false, ex.Message);
    Console.WriteLine(failures == 0 ? "SEMUA TES LULUS" : $"{failures} TES GAGAL");
    return 1;
}

var kiosk = CbtKiosk.AppSettings.Load();
Check("kiosk membaca berkas tool", kiosk.SettingsPath == savedPath, kiosk.SettingsPath);
Check("kiosk: BaseUrl", kiosk.BaseUrl == tool.BaseUrl, kiosk.BaseUrl);
Check("kiosk: StartUrl", kiosk.StartUrl == tool.StartUrl, kiosk.StartUrl);
Check("kiosk: KioskUrl", kiosk.KioskUrl == tool.KioskUrl, kiosk.KioskUrl);
Check("kiosk: FallbackUrl", kiosk.FallbackUrl == tool.FallbackUrl, kiosk.FallbackUrl);
Check("kiosk: FallbackUrl dikenali (HasFallbackUrl)", kiosk.HasFallbackUrl);
Check("kiosk: KioskTimeoutMs", kiosk.KioskTimeoutMs == tool.KioskTimeoutMs, kiosk.KioskTimeoutMs.ToString());
Check("kiosk: KioskAttempts", kiosk.KioskAttempts == tool.KioskAttempts, kiosk.KioskAttempts.ToString());
Check("kiosk: KioskAttemptIntervalMs", kiosk.KioskAttemptIntervalMs == tool.KioskAttemptIntervalMs, kiosk.KioskAttemptIntervalMs.ToString());
Check("kiosk: OfflineFallbackPasswordHash", kiosk.OfflineFallbackPasswordHash == tool.OfflineFallbackPasswordHash);
Check("kiosk: password darurat sendiri dipakai (bukan bawaan)", !kiosk.UsesDefaultEmergencyPassword);
Check("kiosk: AllowDefaultEmergencyPassword=false terbaca", kiosk.AllowDefaultEmergencyPassword == false);
Check("kiosk: hash darurat yang dipakai = punya tool", kiosk.EmergencyPasswordHash == tool.OfflineFallbackPasswordHash);
Check("kiosk: BlockNavigationKeys", kiosk.BlockNavigationKeys == tool.BlockNavigationKeys);
Check("kiosk: AllowZoom", kiosk.AllowZoom == tool.AllowZoom);
Check("kiosk: AllowBackNavigation", kiosk.AllowBackNavigation == tool.AllowBackNavigation);
Check("kiosk: SettingsPasswordHash", kiosk.SettingsPasswordHash == tool.SettingsPasswordHash);
Check("kiosk: ClearSessionOnQuit", kiosk.ClearSessionOnQuit == tool.ClearSessionOnQuit);
Check("kiosk: AutoStart", kiosk.AutoStart == tool.AutoStart);
Check("kiosk: AutoStartAllUsers", kiosk.AutoStartAllUsers == tool.AutoStartAllUsers);
Check("kiosk: StartupMethod", kiosk.StartupMethod == tool.StartupMethod, kiosk.StartupMethod);

Console.WriteLine();
Console.WriteLine("=== 4. Password yang di-hash tool diterima kiosk (alur darurat) ===");
// Force the offline path with an address that can never answer.
kiosk.KioskUrl = "https://127.0.0.1:1/api/kiosk/settings";
kiosk.FallbackUrl = "";
kiosk.KioskTimeoutMs = 700;
kiosk.KioskAttempts = 1;
kiosk.KioskAttemptIntervalMs = 0;

var client = new CbtKiosk.KioskClient(kiosk);
var accepted = client.Validate("rahasia-darurat");
Check("password darurat dari tool DITERIMA kiosk", accepted == CbtKiosk.QuitPasswordResult.Correct, accepted.ToString());

var rejected = client.Validate("bukan-password");
Check("password salah DITOLAK kiosk", rejected == CbtKiosk.QuitPasswordResult.Wrong, rejected.ToString());

Console.WriteLine();
Console.WriteLine("=== 5. Bawaan pabrik lewat berkas yang ditulis tool ===");
var defaulted = new CbtKioskTool.AppSettings { KioskUrl = "https://127.0.0.1:1/api/kiosk/settings" };
SettingsStore.Save(defaulted);
var kioskDefault = CbtKiosk.AppSettings.Load();
kioskDefault.KioskUrl = "https://127.0.0.1:1/api/kiosk/settings";
kioskDefault.KioskAttempts = 1;
kioskDefault.KioskTimeoutMs = 700;
kioskDefault.KioskAttemptIntervalMs = 0;

Check("kiosk memakai bawaan pabrik", kioskDefault.UsesDefaultEmergencyPassword);
Check("bawaan pabrik = " + CbtKiosk.AppSettings.DefaultOfflinePassword,
    CbtKioskTool.Kiosk.DefaultOfflinePassword == CbtKiosk.AppSettings.DefaultOfflinePassword,
    CbtKioskTool.Kiosk.DefaultOfflinePassword);
Check("bawaan pabrik diterima kiosk",
    new CbtKiosk.KioskClient(kioskDefault).Validate(CbtKiosk.AppSettings.DefaultOfflinePassword) == CbtKiosk.QuitPasswordResult.Correct);

Console.WriteLine();
Console.WriteLine("=== 6. Tanggapan endpoint yang dibaca tool ===");
const string live = "{\"success\":true,\"data\":{\"exit_password\":\"Nescafe\",\"password_expires_at\":\"2026-09-28T23:55\",\"is_expired\":false}}";
bool parsed;
CbtKioskTool.KioskPassword password;
string parseError;
parsed = CbtKioskTool.KioskApi.TryParsePassword(live, out password, out parseError);
Check("exit_password terbaca", parsed && password.Password == "Nescafe");
Check("password_expires_at terbaca", parsed && password.ExpiresAt.HasValue);
Check("is_expired=false", parsed && !password.IsExpired);

const string expired = "{\"success\":true,\"data\":{\"exit_password\":\"Nescafe\",\"password_expires_at\":\"2020-01-01T00:00\",\"is_expired\":true}}";
parsed = CbtKioskTool.KioskApi.TryParsePassword(expired, out password, out parseError);
Check("kedaluwarsa dikenali", parsed && password.IsExpiredNow);

const string failed = "{\"success\":false,\"message\":\"belum ada\"}";
parsed = CbtKioskTool.KioskApi.TryParsePassword(failed, out password, out parseError);
Check("success=false ditolak", !parsed, parseError);

const string notJson = "<html>502 Bad Gateway</html>";
parsed = CbtKioskTool.KioskApi.TryParsePassword(notJson, out password, out parseError);
Check("halaman HTML ditolak tanpa crash", !parsed, parseError);

try { Directory.Delete(scratch, true); } catch { /* ignore */ }

Console.WriteLine();
Console.WriteLine(failures == 0 ? "SEMUA TES LULUS" : $"{failures} TES GAGAL");
return failures == 0 ? 0 : 1;
