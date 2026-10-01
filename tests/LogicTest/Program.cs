using CbtKiosk;

var failures = 0;
void Check(string name, bool condition, string detail = "")
{
    Console.WriteLine($"  [{(condition ? "PASS" : "FAIL")}] {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
    if (!condition) failures++;
}

Console.WriteLine("=== 1. Hash ===");
Check("SHA-256(\"abc\") dikenal",
    Hash.Sha256Hex("abc") == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
Check("Hash case-insensitive compare",
    Hash.FixedTimeEquals(Hash.Sha256Hex("abc"), Hash.Sha256Hex("abc")));
Check("FixedTimeEquals beda panjang = false",
    !Hash.FixedTimeEquals("abc", "abcd"));

Console.WriteLine();
Console.WriteLine("=== 2. Password darurat BAWAAN PABRIK ===");
Check($"Bawaan = \"{AppSettings.DefaultOfflinePassword}\"",
    AppSettings.DefaultOfflinePassword == "smkdata2026", AppSettings.DefaultOfflinePassword);
Check("Hash bawaan cocok dengan SHA-256 teksnya",
    AppSettings.DefaultOfflinePasswordHash == Hash.Sha256Hex(AppSettings.DefaultOfflinePassword));

var fresh = new AppSettings
{
    // Alamat yang pasti gagal -> memaksa jalur offline, tanpa setting apa pun.
    KioskUrl = "https://127.0.0.1:1/api/kiosk/settings",
    KioskTimeoutMs = 700,
    KioskAttempts = 1,
    KioskAttemptIntervalMs = 0,
};
Check("Instalasi baru: ada password darurat (bawaan pabrik)", fresh.HasEmergencyPassword);
Check("Instalasi baru: memakai bawaan pabrik", fresh.UsesDefaultEmergencyPassword);
Check("Instalasi baru: belum ada password offline custom", !fresh.HasOfflineFallback);

var freshClient = new KioskClient(fresh);
var f1 = freshClient.Validate(AppSettings.DefaultOfflinePassword);
Check("Server TIDAK terjangkau + password bawaan diterima",
    f1 == QuitPasswordResult.Correct, f1.ToString());

var f2 = freshClient.Validate("ngawur");
Check("Server TIDAK terjangkau + password salah ditolak",
    f2 == QuitPasswordResult.Wrong, f2.ToString());

Console.WriteLine();
Console.WriteLine("=== 3. Password darurat OFFLINE buatan pengawas ===");
var offline = new AppSettings
{
    KioskUrl = "https://127.0.0.1:1/api/kiosk/settings",
    KioskTimeoutMs = 700,
    KioskAttempts = 1,
    KioskAttemptIntervalMs = 0,
    OfflineFallbackPasswordHash = Hash.Sha256Hex("darurat123"),
};
var offlineClient = new KioskClient(offline);

var o1 = offlineClient.Validate("darurat123");
Check("Password darurat sendiri BENAR diterima", o1 == QuitPasswordResult.Correct, o1.ToString());

var o2 = offlineClient.Validate("salah");
Check("Password darurat sendiri SALAH ditolak", o2 == QuitPasswordResult.Wrong, o2.ToString());

var o3 = offlineClient.Validate(AppSettings.DefaultOfflinePassword);
Check("Bawaan pabrik TIDAK berlaku bila pengawas sudah mengatur sendiri",
    o3 == QuitPasswordResult.Wrong, o3.ToString());

Console.WriteLine();
Console.WriteLine("=== 4. Bawaan pabrik dimatikan dan tidak ada password custom ===");
var none = new AppSettings
{
    KioskUrl = "https://127.0.0.1:1/api/kiosk/settings",
    KioskTimeoutMs = 700,
    KioskAttempts = 1,
    KioskAttemptIntervalMs = 0,
    AllowDefaultEmergencyPassword = false,
};
Check("Tidak ada password darurat sama sekali", !none.HasEmergencyPassword);

var noneClient = new KioskClient(none);
var n1 = noneClient.Validate("apa saja");
Check("Ditolak sebagai Unreachable", n1 == QuitPasswordResult.Unreachable, n1.ToString());

var n2 = noneClient.Validate(AppSettings.DefaultOfflinePassword);
Check("Bawaan pabrik ditolak setelah dimatikan", n2 == QuitPasswordResult.Unreachable, n2.ToString());

Console.WriteLine();
Console.WriteLine("=== 5. Endpoint CBT asli (online) ===");
// Uji daring memakai endpoint sungguhan; kejadian jaringan tidak boleh menggagalkan rilis.
var online = new AppSettings
{
    KioskUrl = "https://cbt.smkdata.sch.id/api/kiosk/settings",
    KioskTimeoutMs = 8000,
    KioskAttempts = 2,
    KioskAttemptIntervalMs = 500,
    // Password darurat untuk membuktikan bawaan tetap berlaku walau server hidup.
    AllowDefaultEmergencyPassword = true,
};
var client = new KioskClient(online);

var fetched = client.TryFetch(online.KioskUrl, out var kp);
Check("Endpoint terjangkau & respons terparse", fetched,
    fetched ? $"password tersedia, expired={kp.IsExpired}, expires={kp.ExpiresAt:yyyy-MM-dd HH:mm}" : "tidak terjangkau");

if (fetched && !kp.IsExpired && !string.IsNullOrEmpty(kp.Password))
{
    var r1 = client.Validate(kp.Password);
    Check("Password ONLINE benar diterima", r1 == QuitPasswordResult.Correct, r1.ToString());

    var r2 = client.Validate(kp.Password + "x");
    Check("Password ONLINE salah ditolak", r2 == QuitPasswordResult.Wrong, r2.ToString());

    var r3 = client.Validate(kp.Password.ToUpperInvariant() == kp.Password ? kp.Password + "!" : kp.Password.ToUpperInvariant());
    Check("Password ONLINE beda huruf besar/kecil ditolak (case-sensitive)",
        kp.Password.ToUpperInvariant() == kp.Password || r3 == QuitPasswordResult.Wrong, r3.ToString());

    // Inti permintaan: password darurat tetap bisa dipakai walau server hidup.
    var r4 = client.Validate(AppSettings.DefaultOfflinePassword);
    Check("Password DARURAT tetap diterima walau server hidup & password online valid",
        r4 == QuitPasswordResult.Correct, r4.ToString());
}
else if (fetched && kp.IsExpired)
{
    // Justru kasus yang diminta: online kedaluwarsa -> password darurat harus tetap jalan.
    var r5 = client.Validate(AppSettings.DefaultOfflinePassword);
    Check("Password ONLINE kedaluwarsa + password DARURAT tetap diterima",
        r5 == QuitPasswordResult.Correct, r5.ToString());

    var r6 = client.Validate("jelas-salah");
    Check("Password ONLINE kedaluwarsa + password salah -> alasan 'kedaluwarsa'",
        r6 == QuitPasswordResult.Expired, r6.ToString());
}
else
{
    Console.WriteLine("  (dilewati: endpoint mengembalikan password kosong / tidak terparse)");
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "SEMUA TES LULUS" : $"{failures} TES GAGAL");
return failures == 0 ? 0 : 1;
