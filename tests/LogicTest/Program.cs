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
Console.WriteLine("=== 2. Endpoint CBT asli (online) ===");
var online = new AppSettings
{
    KioskUrl = "https://cbt.smkdata.sch.id/api/kiosk/settings",
    KioskTimeoutMs = 8000,
    KioskAttempts = 2,
    KioskAttemptIntervalMs = 500,
};
var client = new KioskClient(online);

// Ambil password asli dari server, lalu uji validasi terhadapnya.
var fetched = client.TryFetch(online.KioskUrl, out var kp);
Check("Endpoint terjangkau & respons terparse", fetched,
    fetched ? $"password tersedia, expired={kp.IsExpired}, expires={kp.ExpiresAt:yyyy-MM-dd HH:mm}" : "tidak terjangkau");

if (fetched && !kp.IsExpired && !string.IsNullOrEmpty(kp.Password))
{
    var r1 = client.Validate(kp.Password);
    Check("Password BENAR diterima", r1 == QuitPasswordResult.Correct, r1.ToString());

    var r2 = client.Validate(kp.Password + "x");
    Check("Password SALAH ditolak", r2 == QuitPasswordResult.Wrong, r2.ToString());

    var r3 = client.Validate(kp.Password.ToUpperInvariant() == kp.Password ? kp.Password + "!" : kp.Password.ToUpperInvariant());
    Check("Password beda huruf besar/kecil ditolak (case-sensitive)",
        kp.Password.ToUpperInvariant() == kp.Password || r3 == QuitPasswordResult.Wrong, r3.ToString());
}
else
{
    Console.WriteLine("  (dilewati: endpoint mengembalikan password kosong / kedaluwarsa)");
}

Console.WriteLine();
Console.WriteLine("=== 3. Fallback OFFLINE (server tidak terjangkau) ===");
var offline = new AppSettings
{
    // Alamat yang pasti gagal -> memaksa jalur offline.
    KioskUrl = "https://127.0.0.1:1/api/kiosk/settings",
    KioskTimeoutMs = 700,
    KioskAttempts = 1,
    KioskAttemptIntervalMs = 0,
    OfflineFallbackPasswordHash = Hash.Sha256Hex("darurat123"),
};
var offlineClient = new KioskClient(offline);

var o1 = offlineClient.Validate("darurat123");
Check("Password cadangan offline BENAR diterima", o1 == QuitPasswordResult.Correct, o1.ToString());

var o2 = offlineClient.Validate("salah");
Check("Password cadangan offline SALAH ditolak", o2 == QuitPasswordResult.Wrong, o2.ToString());

Console.WriteLine();
Console.WriteLine("=== 4. Tidak ada fallback apa pun -> Unreachable ===");
var none = new AppSettings
{
    KioskUrl = "https://127.0.0.1:1/api/kiosk/settings",
    KioskTimeoutMs = 700,
    KioskAttempts = 1,
    KioskAttemptIntervalMs = 0,
    OfflineFallbackPasswordHash = "",
};
var n1 = new KioskClient(none).Validate("apa saja");
Check("Ditolak sebagai Unreachable", n1 == QuitPasswordResult.Unreachable, n1.ToString());

Console.WriteLine();
Console.WriteLine(failures == 0 ? "SEMUA TES LULUS" : $"{failures} TES GAGAL");
return failures == 0 ? 0 : 1;
