# CBT Kiosk — Aplikasi Windows Mode Ujian

Aplikasi desktop Windows yang **mengunci komputer siswa agar fokus pada ujian CBT**.
Saat dijalankan, aplikasi membuka URL ujian secara penuh layar dan memblokir jalan keluar
(Alt+Tab, tombol Windows, Alt+F4, dsb). Untuk keluar, pengawas memasukkan **password yang
diambil dari server CBT** — dilengkapi **password darurat (offline)** yang **selalu bisa dipakai**
walau internet/server mati maupun **password online sudah kedaluwarsa**.

> **Bukan Safe Exam Browser.** Ini aplikasi mandiri yang dibuat khusus untuk
> `cbt.smkdata.sch.id`, dibangun dengan .NET 8 (WinForms) + WebView2.

---

## 1. Fitur

| Fitur | Keterangan |
|---|---|
| **Aplikasi pengaturan RINGAN** | `CbtKioskTool.exe` — **hanya ± 50 KB** (dulu ± 63 MB). Satu jendela: alamat ujian, tes koneksi, password darurat, password pengaturan, penguncian, dan auto-start. Berjalan dengan .NET Framework yang sudah ada di Windows, jadi **tetap tanpa instalasi**. |
| **Buka URL ujian** | URL `https://cbt.smkdata.sch.id` sudah tertanam; bisa diubah di Pengaturan. |
| **Mode kios penuh layar** | Tanpa bingkai, selalu di atas, maksimal, auto-fokus. |
| **Pintasan keyboard** | **F5** = muat ulang halaman ujian, **Ctrl+Alt+Q** = keluar (minta password). Selalu aktif — bekerja walaupun halaman ujian sedang difokus. |
| **Lockdown** | Blokir Alt+Tab, Win, Alt+F4, Alt+Esc, Ctrl+Esc, Ctrl+Shift+Esc, F12, Ctrl+P/S/O/N/T/W/U/J/H, klik-kanan, DevTools, unduhan, popup, izin kamera/mikrofon/notifikasi, keluar paksa. |
| **Batasi navigasi** | Hanya boleh membuka domain CBT (dan domain fallback yang dikonfigurasi). |
| **Password keluar dari API CBT** | Diambil dari `https://cbt.smkdata.sch.id/api/kiosk/settings`. |
| **Fallback online** | Endpoint kedua (opsional) dicoba bila endpoint utama tidak terjawab. |
| **Password darurat offline (jaring pengaman)** | Password yang tersimpan di komputer dan **selalu diterima** untuk keluar: saat server tidak terjangkau, saat password online **kedaluwarsa**, maupun sebagai jaring pengaman terakhir saat server hidup. Sejak v1.7.0 sudah terisi **bawaan pabrik** sehingga komputer baru pun tidak bisa terkunci. Dapat diganti atau dimatikan di Pengaturan. |
| **Hapus sesi saat keluar** | Cookies & data situs dihapus saat keluar (dan sisa data dibersihkan saat start), sehingga siswa **wajib login ulang** tiap sesi. Bisa dimatikan di Pengaturan. |
| **Auto-start saat Windows menyala** | Opsi di Pengaturan untuk menjalankan aplikasi otomatis saat Windows login (per-pengguna, atau semua pengguna bila dijalankan sebagai Administrator). |
| **Password darurat bawaan** | `smkdata2026` — langsung aktif pada instalasi baru; ganti di Pengaturan (atau matikan) agar hanya pengawas yang tahu. |
| **Pengaturan terproteksi** | Menu Pengaturan bisa dikunci dengan password agar tidak dibuka siswa. |
| **Log** | Semua kejadian penting dicatat untuk audit/pengawas. |

### Urutan pemeriksaan password keluar

1. **Password ONLINE** — endpoint utama `cbtKioskURL` (dicoba beberapa kali, default 3×), lalu
   **fallback online** `cbtFallbackURL` bila dikonfigurasi dan endpoint utama gagal.
   - Cocok → kiosk keluar.
   - Tidak cocok → ditolak sebagai *"password salah"* (server jadi penentu utama).
2. **Password DARURAT (offline)** — password lokal di komputer. Diterima bila:
   - tidak ada endpoint yang terjangkau (internet/server mati), **atau**
   - endpoint menjawab tetapi password online **kedaluwarsa** / kosong, **atau**
   - endpoint menjawab dengan password valid namun yang dimasukkan bukan itu
     *(jaring pengaman terakhir — pengawas tidak pernah terkunci total)*.

Bila password darurat belum pernah diatur, yang berlaku adalah **bawaan pabrik**
(`smkdata2026`). Bawaan ini **otomatis berhenti berlaku** begitu pengawas menyimpan password
darurat sendiri, dan bisa dimatikan total lewat centang di tab Keamanan.

---

## 2. Kontrak API

Aplikasi mengharapkan respons JSON seperti ini dari endpoint:

```json
{
  "success": true,
  "data": {
    "exit_password": "Nescafe",
    "password_expires_at": "2026-09-28T23:55",
    "is_expired": false
  }
}
```

Aturan:

- `success != true` atau `data` tidak ada → dianggap gagal.
- `is_expired == true` → password online **ditolak** dengan pesan *"password kedaluwarsa"*,
  **tetapi password darurat offline tetap bisa dipakai** untuk keluar.
- `is_expired == false` tetapi `password_expires_at` sudah lewat → tetap ditolak (pemeriksaan pengaman).
- `exit_password` kosong → ditolak sebagai *tidak tersedia*.
- Selain itu, password yang diketik dibandingkan **persis (case-sensitive)** dengan `exit_password`.

---

## 3. Cara pakai

### 3.1 Prasyarat di komputer siswa

- Windows 10 (1803) atau lebih baru / Windows 11.
- **Microsoft Edge WebView2 Runtime** — sudah ada di sebagian besar Windows 10/11; bila belum,
  pasang sekali dari <https://go.microsoft.com/fwlink/p/?LinkId=2124703>.

Aplikasi ujian (`CbtKiosk.exe`) bersifat **portabel** (self-contained): tidak perlu memasang .NET.
Aplikasi pengaturan (`CbtKioskTool.exe`) hanya ± 50 KB dan memakai .NET Framework 4.6.2 yang
**sudah ada** di Windows 10 (1803+) dan Windows 11 — juga tidak perlu memasang apa pun.

### 3.2 Mengatur aplikasi (dilakukan pengawas/admin)

Cara termudah: gunakan **aplikasi pengaturan ringan** `CbtKioskTool.exe` — tidak perlu console lagi.

1. Klik kanan **`CbtKioskTool.exe`** → **Run as administrator** (agar pengaturan bisa disimpan
   untuk semua pengguna di `%ProgramData%`).
2. Isi pada tab yang tersedia:

   | Tab | Isi |
   |---|---|
   | **Umum & Koneksi** | **URL ujian** (yang dibuka saat ujian, default `https://cbt.smkdata.sch.id`), **Base URL** (domain yang diizinkan), **Endpoint password** (default `https://cbt.smkdata.sch.id/api/kiosk/settings`), **Fallback online** (opsional), pengaturan timeout/percobaan. |
   | **Keamanan & Penguncian** | **Password darurat (offline)** + centang "pakai bawaan pabrik", **Password pengaturan**, opsi lockdown (blokir Alt+Tab/Win, zoom, tombol kembali, hapus sesi saat keluar). |
   | **Startup & Aplikasi** | Lokasi `CbtKiosk.exe`, **auto-start saat Windows menyala** (pengguna ini atau semua pengguna, registry atau Scheduled Task), Pemeliharaan, cek WebView2. |
   | **Bantuan** | Ringkasan langkah, lokasi berkas pengaturan, pintasan keyboard. |

3. Klik **Tes koneksi** untuk memastikan endpoint terjangkau.
4. Klik **Simpan**. Setelah tersimpan, pengaturan langsung berlaku untuk `CbtKiosk.exe`.

Tombol lain: **Muat ulang** (baca ulang berkas), **Bawaan** (kembalikan nilai pabrik),
**Buka folder log**, **Periksa startup**, **Jalankan CBT Kiosk**.

> Bila dijalankan tanpa hak Administrator, aplikasi menampilkan tombol
> **"Jalankan sebagai Administrator"** dan hanya dapat menyimpan pengaturan per-pengguna.

<details>
<summary><b>Cara lama (masih didukung): <code>CbtKiosk.exe --settings</code></b></summary>

1. Klik kanan `CbtKiosk.exe` → **Run as administrator** → jalankan dengan argumen `--settings`:

   ```
   CbtKiosk.exe --settings
   ```

   (Cara mudah: buat shortcut dengan Target `"C:\lokasi\CbtKiosk.exe" --settings`,
   lalu klik kanan shortcut → Run as administrator.)

2. Isi kolom yang sama seperti di atas, klik **Simpan**.

Tampilan pengaturan di dalam kiosk (tray → *Buka Pengaturan*) tetap tersedia dan memakai
jendela yang sama.
</details>

> Tombol **Simpan** ada di bar bawah jendela Pengaturan (selalu terlihat). Isi pengaturan bisa
> di-scroll bila layar kecil.

Pengaturan disimpan di:

```
C:\ProgramData\CbtKiosk\settings.json        (semua pengguna — perlu Administrator, disarankan)
C:\Users\<user>\AppData\Roaming\CbtKiosk\settings.json   (hanya pengguna ini — tanpa admin)
```

### 3.3 Menjalankan ujian

- Siswa cukup **klik dua kali** `CbtKiosk.exe`. Aplikasi membuka URL ujian dalam mode kios.
- **Pintasan keyboard** (selalu aktif, dari dalam aplikasi):

  | Tombol | Fungsi |
  |---|---|
  | **F5** | Muat ulang halaman ujian |
  | **Ctrl + Alt + Q** | Keluar dari ujian (muncul dialog password) |

- **Keluar dari ujian:** tekan **Ctrl+Alt+Q** → masukkan password dari panel CBT.
  - Bila internet/server mati atau password online **kedaluwarsa**, masukkan **password darurat
    offline** (bawaan `smkdata2026` bila belum pernah diganti). Lihat bagian C panduan pengawas.
  - Alternatif: klik kanan ikon aplikasi di **system tray** (pojok kanan bawah) →
    *Keluar dari ujian…*. Menu **Buka Pengaturan** juga ada di tray (dilindungi password pengaturan, bila diatur).
- Setelah keluar, **sesi/cookies dihapus**, jadi saat aplikasi dibuka lagi siswa **harus login ulang**.

### 3.4 Menjadikan aplikasi sebagai shell (kios paling kuat, opsional)

Agar siswa benar-benar tidak bisa keluar tanpa password, ganti shell akun siswa dari
`explorer.exe` ke aplikasi ini (Windows otomatis keluar setelah aplikasi ditutup). Cocok untuk
akun khusus ujian. Lihat `docs/PANDUAN-PENGAWAS.md` bagian "Mode Kios Kuat".

### 3.5 Menjalankan otomatis saat Windows menyala

Di aplikasi pengaturan (`CbtKioskTool.exe`) → bagian **"Startup Windows dan aplikasi kiosk"**:

1. Centang **"Jalankan otomatis saat Windows menyala"**.
2. Pilih cakupan:
   - **tidak** centang *"Untuk SEMUA pengguna Windows"* → hanya pengguna yang sedang login
     (ditulis ke `HKCU\...\Run`, **tidak** perlu Administrator).
   - centang *"Untuk SEMUA pengguna Windows"* → semua akun (ditulis ke `HKLM\...\Run`,
     **perlu dijalankan sebagai Administrator**).
3. Klik **Simpan**. Status registry ditampilkan di bawah kotak centang.

Aplikasi akan otomatis terbuka (mode kios) setiap kali Windows login. Untuk mematikan, hilangkan
centang lalu **Simpan**.

> Bila opsi "semua pengguna" gagal (tanpa hak Administrator), aplikasi menampilkan peringatan —
> pengaturan lain tetap tersimpan.

## 4. Membangun sendiri

Perlu .NET SDK 8.0. Perintah berikut menghasilkan **dua** berkas: aplikasi ujian dan aplikasi
pengaturan.

```bash
# 1) Aplikasi ujian (mode kios) - self-contained .NET 8
dotnet publish src/CbtKiosk/CbtKiosk.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o publish/win-x64

# 2) Aplikasi pengaturan RINGAN - .NET Framework 4.6.2 (sudah ada di Windows)
dotnet build src/CbtKioskTool/CbtKioskTool.csproj \
  -c Release -o publish/tool
```

Hasil:

| Berkas | Fungsi | Ukuran |
|---|---|---|
| `publish/win-x64/CbtKiosk.exe` | Aplikasi ujian (mode kios). | ± 63 MB |
| `publish/tool/CbtKioskTool.exe` | Aplikasi pengaturan ringan. | **± 50 KB** |

Keduanya cukup dijalankan — tidak perlu memasang .NET, dan aplikasi pengaturnya memakai .NET
Framework yang sudah tersedia di Windows 10/11.

> Build juga bisa dijalankan di Linux/macOS berkat `EnableWindowsTargeting=true`
> (hanya menghasilkan berkas Windows, tidak bisa dijalankan di sana).

### 4.1 Rilis otomatis (GitHub Actions)

Workflow `.github/workflows/build.yml` membangun kedua aplikasi pada setiap push ke
`master`/`main`, pada tag `v*`, dan pada *Run workflow* manual. Setelah build sukses, hasilnya
otomatis **dipublikasikan sebagai Release**:

- Push: tag `v<versi>-<7 digit commit>`; tag `v<versi>` bila didorong dengan tag `v*`.
- Aset rilis: `CbtKiosk.exe`, `CbtKioskTool.exe`, dan `CbtKiosk-win-x64-<versi>.zip`.
- Nomor versi diambil dari `<Version>` di `src/CbtKiosk/CbtKiosk.csproj`.
- Tag berbasis commit (bukan nomor build) membuat rilis tetap satu bila GitHub menjalankan dua
  kali untuk satu push.

Untuk membuat rilis bernomor versi resmi (mis. `v1.6.0`): ubah `<Version>` lalu
`git tag v1.6.0 && git push origin v1.6.0`.

---

## 5. Keterbatasan yang perlu diketahui

- **Ctrl+Alt+Del tidak bisa diblokir** oleh aplikasi biasa (itu Secure Attention Sequence milik
  Windows). Karena itu, untuk penguncian penuh gunakan **akun siswa terbatas (non-administrator)**
  + opsi *shell replacement* di atas, dan/atau kebijakan grup Windows.
- Aplikasi **tidak mengubah pengaturan sistem secara permanen**, kecuali satu kunci sementara
  `DisableTaskMgr` untuk pengguna saat ini yang **dikembalikan saat keluar**.
- Ini aplikasi pihak ketiga; **bukan** SEB dan tidak memiliki sertifikat digital. Windows
  SmartScreen mungkin menampilkan peringatan saat pertama dijalankan
  (*More info → Run anyway*).

---

## 6. Struktur proyek

```
cbt-kiosk/
├─ src/CbtKiosk/
│  ├─ Program.cs          # titik masuk, deteksi --settings, single-instance, bersih-bersih sesi
│  ├─ MainForm.cs         # jendela kios, WebView2, lockdown, pintasan keyboard, hapus sesi
│  ├─ SettingsForm.cs     # jendela pengaturan di dalam kios (dipakai tray & --settings)
│  ├─ AutoStart.cs        # daftar/hapus auto-start di registry / Scheduled Task
│  ├─ PasswordPrompt.cs   # dialog password
│  ├─ KioskClient.cs      # HTTP ke endpoint CBT + validasi password + fallback
│  ├─ AppSettings.cs      # model pengaturan + baca/tulis JSON
│  ├─ Hash.cs             # SHA-256
│  ├─ Native.cs           # P/Invoke (keyboard hook)
│  └─ Logger.cs           # log berkas
├─ src/CbtKioskTool/      # APLIKASI PENGATURAN RINGAN (± 50 KB, .NET Framework)
│  ├─ Program.cs          # titik masuk, --kiosk/--reset/--help
│  ├─ MainForm.cs         # jendela pengaturan (alamat, password, penguncian, startup)
│  ├─ AppSettings.cs      # model pengaturan (Kiosk.cs) - nama properti sama dengan kiosk
│  ├─ SettingsStore.cs    # baca/tulis settings.json di lokasi yang sama dengan kiosk
│  ├─ Json.cs             # pembaca/penulis JSON sendiri (tanpa pustaka) demi ukuran kecil
│  ├─ AutoStart.cs        # registry Run / Scheduled Task (kompatibel dengan kiosk)
│  └─ KioskApi.cs         # tes koneksi endpoint + pemeriksaan password online
├─ tests/LogicTest/       # uji logika lintas-platform (hash, endpoint, fallback offline)
├─ tests/ToolTest/        # uji tool: tulis settings.json, lalu dibaca ulang oleh kode kiosk asli
├─ .github/workflows/build.yml
├─ docs/PANDUAN-PENGAWAS.md
└─ README.md
```

## 7. Lisensi

MIT. Dibuat untuk keperluan internal sekolah.
