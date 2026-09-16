# CBT Kiosk — Aplikasi Windows Mode Ujian

Aplikasi desktop Windows yang **mengunci komputer siswa agar fokus pada ujian CBT**.
Saat dijalankan, aplikasi membuka URL ujian secara penuh layar dan memblokir jalan keluar
(Alt+Tab, tombol Windows, Alt+F4, dsb). Untuk keluar, pengawas memasukkan **password yang
diambil dari server CBT** — dengan **password cadangan offline** bila internet terputus.

> **Bukan Safe Exam Browser.** Ini aplikasi mandiri yang dibuat khusus untuk
> `cbt.smkdata.sch.id`, dibangun dengan .NET 8 (WinForms) + WebView2.

---

## 1. Fitur

| Fitur | Keterangan |
|---|---|
| **Buka URL ujian** | URL `https://cbt.smkdata.sch.id` sudah tertanam; bisa diubah di Pengaturan. |
| **Mode kios penuh layar** | Tanpa bingkai, selalu di atas, maksimal, auto-fokus. |
| **Tombol menu samping** | Tab melayang di tepi kanan layar (☰). Diklik dulu → muncul menu **Muat ulang** & **Keluar dari ujian**. Selalu terlihat di atas halaman ujian. |
| **Lockdown** | Blokir Alt+Tab, Win, Alt+F4, Alt+Esc, Ctrl+Esc, Ctrl+Shift+Esc, F12, Ctrl+P/S/O/N/T/W/U/J/H, klik-kanan, DevTools, unduhan, popup, izin kamera/mikrofon/notifikasi, keluar paksa. |
| **Batasi navigasi** | Hanya boleh membuka domain CBT (dan domain fallback yang dikonfigurasi). |
| **Password keluar dari API CBT** | Diambil dari `https://cbt.smkdata.sch.id/api/kiosk/settings`. |
| **Fallback online** | Endpoint kedua (opsional) dicoba bila endpoint utama tidak terjawab. |
| **Fallback offline** | Password cadangan yang disimpan di komputer — dipakai **hanya** bila server sama sekali tidak terjangkau. Diatur di menu Pengaturan. |
| **Hapus sesi saat keluar** | Cookies & data situs dihapus saat keluar (dan sisa data dibersihkan saat start), sehingga siswa **wajib login ulang** tiap sesi. Bisa dimatikan di Pengaturan. |
| **Pengaturan terproteksi** | Menu Pengaturan bisa dikunci dengan password agar tidak dibuka siswa. |
| **Log** | Semua kejadian penting dicatat untuk audit/pengawas. |

### Urutan pemeriksaan password keluar

1. **Endpoint utama** `cbtKioskURL` — dicoba beberapa kali (default 3×).
2. **Fallback online** `cbtFallbackURL` — bila dikonfigurasi dan endpoint utama gagal.
3. **Fallback offline** — password cadangan lokal, **hanya** bila tidak ada endpoint yang terjangkau.

Saat server menjawab, password dari panel CBT **selalu menang** atas password cadangan.

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
- `is_expired == true` → password **ditolak** dengan pesan *"password kedaluwarsa"*.
- `is_expired == false` tetapi `password_expires_at` sudah lewat → tetap ditolak (pemeriksaan pengaman).
- `exit_password` kosong → ditolak sebagai *tidak tersedia*.
- Selain itu, password yang diketik dibandingkan **persis (case-sensitive)** dengan `exit_password`.

---

## 3. Cara pakai

### 3.1 Prasyarat di komputer siswa

- Windows 10 (1803) atau lebih baru / Windows 11.
- **Microsoft Edge WebView2 Runtime** — sudah ada di sebagian besar Windows 10/11; bila belum,
  pasang sekali dari <https://go.microsoft.com/fwlink/p/?LinkId=2124703>.

Aplikasi bersifat **portabel** (self-contained): tidak perlu memasang .NET.

### 3.2 Mengatur aplikasi (dilakukan pengawas/admin)

1. Klik kanan `CbtKiosk.exe` → **Run as administrator** → jalankan dengan argumen `--settings`:

   ```
   CbtKiosk.exe --settings
   ```

   (Cara mudah: buat shortcut dengan Target `"C:\lokasi\CbtKiosk.exe" --settings`,
   lalu klik kanan shortcut → Run as administrator.)

2. Isi:
   - **URL ujian** (yang dibuka saat ujian) — default `https://cbt.smkdata.sch.id`.
   - **Base URL** — domain yang diizinkan.
   - **Endpoint password** — default `https://cbt.smkdata.sch.id/api/kiosk/settings`.
   - **Fallback online** — opsional, mis. endpoint cadangan bila ada.
   - **Password cadangan offline** — isi bila ingin ada password darurat saat internet mati.
   - **Password pengaturan** — isi agar siswa tidak bisa membuka Pengaturan dari dalam ujian.
3. Klik **Tes koneksi** untuk memastikan endpoint terjangkau.
4. Klik **Simpan**.

Pengaturan disimpan di:

```
C:\ProgramData\CbtKiosk\settings.json        (semua pengguna — perlu Administrator, disarankan)
C:\Users\<user>\AppData\Roaming\CbtKiosk\settings.json   (hanya pengguna ini — tanpa admin)
```

### 3.3 Menjalankan ujian

- Siswa cukup **klik dua kali** `CbtKiosk.exe`. Aplikasi membuka URL ujian dalam mode kios.
- **Keluar dari ujian:** klik **tab menu** (☰) di **tepi kanan layar** → menu terbuka →
  klik **"Keluar dari ujian"**, lalu masukkan password.
  - Menu yang sama juga punya tombol **"Muat ulang"** untuk memuat ulang halaman ujian.
  - Alternatif: klik kanan ikon aplikasi di **system tray** (pojok kanan bawah) → *Keluar dari ujian…*.
  - Menu **Buka Pengaturan** ada di tray (dilindungi password pengaturan, bila diatur).
- Setelah keluar, **sesi/cookies dihapus**, jadi saat aplikasi dibuka lagi siswa **harus login ulang**.

### 3.4 Menjadikan aplikasi sebagai shell (kios paling kuat, opsional)

Agar siswa benar-benar tidak bisa keluar tanpa password, ganti shell akun siswa dari
`explorer.exe` ke aplikasi ini (Windows otomatis keluar setelah aplikasi ditutup). Cocok untuk
akun khusus ujian. Lihat `docs/PANDUAN-PENGAWAS.md` bagian "Mode Kios Kuat".

---

## 4. Membangun sendiri

Perlu .NET SDK 8.0.

```bash
dotnet publish src/CbtKiosk/CbtKiosk.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o publish/win-x64
```

Hasil: `publish/win-x64/CbtKiosk.exe` (± 70–90 MB, satu berkas, portabel).

> Build juga bisa dijalankan di Linux/macOS berkat `EnableWindowsTargeting=true`
> (hanya menghasilkan berkas Windows, tidak bisa dijalankan di sana).

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
│  ├─ MainForm.cs         # jendela kios, WebView2, lockdown, alur keluar, hapus sesi
│  ├─ SidebarForm.cs      # tab menu melayang (Muat ulang / Keluar)
│  ├─ SettingsForm.cs     # jendela pengaturan (URL, password, lockdown)
│  ├─ PasswordPrompt.cs   # dialog password
│  ├─ KioskClient.cs      # HTTP ke endpoint CBT + validasi password + fallback
│  ├─ AppSettings.cs      # model pengaturan + baca/tulis JSON
│  ├─ Hash.cs             # SHA-256
│  ├─ Native.cs           # P/Invoke (keyboard hook)
│  └─ Logger.cs           # log berkas
├─ .github/workflows/build.yml
├─ docs/PANDUAN-PENGAWAS.md
└─ README.md
```

## 7. Lisensi

MIT. Dibuat untuk keperluan internal sekolah.
