# Panduan Pengawas — CBT Kiosk

Panduan singkat untuk pengawas/guru yang mengawasi ujian.

---

## A. Sebelum ujian (persiapan)

1. **Pastikan WebView2 terpasang.** Di komputer siswa, buka salah satu:
   - *Settings → Apps* dan cari "WebView2", atau
   - jalankan `CbtKiosk.exe`. Bila muncul pesan "Tidak dapat memulai komponen browser",
     pasang WebView2 Runtime dari <https://go.microsoft.com/fwlink/p/?LinkId=2124703>.

2. **Atur aplikasi (sekali saja, sebagai Administrator).**
   - Klik kanan `CbtKiosk.exe` → *Run as administrator*, dengan argumen `--settings`.
   - Periksa **URL ujian** dan **Endpoint password** sudah benar.
   - Klik **Tes koneksi** → harus muncul "OK — endpoint terjangkau".
   - Isi **Password cadangan offline** (lihat bagian C) bila perlu.
   - Isi **Password pengaturan** bila ingin siswa tidak bisa membuka Pengaturan.
   - **Simpan**.

3. **Uji coba** di satu komputer: jalankan `CbtKiosk.exe`, pastikan halaman ujian terbuka penuh
   layar, lalu coba keluar dengan password dari panel CBT.

---

## B. Saat ujian

1. Siswa **klik dua kali** `CbtKiosk.exe`.
2. Aplikasi membuka halaman ujian, penuh layar, terkunci.
3. Siswa mengerjakan ujian. Tombol keluar paksa (Alt+Tab, Win, Alt+F4, dsb.) diblokir.

### Mengeluarkan siswa dari mode ujian

1. Tekan **Ctrl + Alt + Q** (dari dalam aplikasi; bekerja walau halaman ujian difokus).
2. Masukkan **password** (password yang dikelola di panel CBT).
3. Aplikasi menutup dan mengembalikan desktop.

> Untuk **memuat ulang** halaman ujian, tekan **F5**.

> Alternatif: klik kanan ikon **CBT Kiosk** di **system tray** (pojok kanan bawah, mungkin
> tersembunyi di panah ▲) → **Keluar dari ujian (Ctrl+Alt+Q)…**.

> Bila password salah/kedaluwarsa, aplikasi menampilkan alasannya:
> *"Password salah"*, *"Password sudah kedaluwarsa"*, atau *"Tidak dapat menghubungi server"*.

> **Sesi dihapus saat keluar.** Cookies dan data situs dibersihkan, sehingga saat aplikasi
> dibuka lagi siswa **harus login ulang**. Ini diatur oleh opsi *"Hapus sesi/cookies saat keluar"*
> di Pengaturan (aktif secara bawaan).

---

## C. Password cadangan offline (bila internet/server mati)

Fitur ini untuk keadaan darurat: server CBT tidak bisa dihubungi sama sekali, sehingga password
dari panel tidak bisa diverifikasi. Password cadangan **disimpan di komputer** dan hanya dipakai
bila **tidak ada endpoint yang terjangkau**.

### Mengatur password cadangan offline

1. Buka **Pengaturan** (sebagai Administrator, `CbtKiosk.exe --settings`).
2. Di bagian **"Password cadangan offline"**, ketik password darurat, ulangi, lalu **Simpan**.
   - Password disimpan sebagai **hash SHA-256** (tidak dalam bentuk teks biasa).
3. Beri tahu password ini **hanya kepada pengawas** yang berwenang.

### Menghapus password cadangan

Buka Pengaturan → bagian *Password cadangan offline* → tombol **Hapus** → Simpan.

> **Keamanan:** jaga kerahasiaan password cadangan. Siapa pun yang tahu password ini bisa keluar
> dari mode ujian meski server mati.

---

## D. Mode Kios Kuat (opsional, penguncian maksimal)

Untuk penguncian terkuat, jalankan aplikasi ini sebagai **shell** akun siswa, sehingga tidak ada
desktop/Explorer yang bisa dipakai untuk keluar.

### D.1 Siapkan akun khusus ujian

Buat akun Windows **standar (non-administrator)** khusus untuk ujian, mis. `siswa`.

### D.2 Ganti shell akun tersebut

Jalankan sebagai Administrator, ganti `<USERNAME>` dengan nama akun siswa:

```cmd
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon" /v Shell /t REG_SZ /d "C:\CbtKiosk\CbtKiosk.exe" /f
```

Ini mengubah shell untuk **semua pengguna**. Untuk membatasi hanya pada akun tertentu, gunakan
**Group Policy**:

- *Computer Configuration → Administrative Templates → System → Custom User Interface*
  → set ke `C:\CbtKiosk\CbtKiosk.exe`, lalu terapkan ke akun siswa.

Setelah siswa menutup aplikasi (dengan password), Windows akan **log out otomatis**.

### D.3 Mengembalikan Explorer (bila perlu)

```cmd
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon" /v Shell /t REG_SZ /d "explorer.exe" /f
```

---

## E. Pemecahan masalah

| Gejala | Penyebab & solusi |
|---|---|
| "Tidak dapat memulai komponen browser (WebView2)" | WebView2 Runtime belum terpasang. Pasang dari tautan di bagian A.1. |
| Halaman ujian tidak muncul / kosong | Periksa koneksi internet & **URL ujian** di Pengaturan. |
| "Password salah" padahal benar | Pastikan password di panel CBT sudah diperbarui dan belum kedaluwarsa; perhatikan **huruf besar/kecil**. |
| "Password sudah kedaluwarsa" | Perbarui password di panel CBT (menu pengaturan kios). |
| "Tidak dapat menghubungi server CBT…" | Internet/server mati dan **password cadangan offline belum diatur**. Atur lewat Pengaturan (bagian C). |
| Tidak bisa menyimpan Pengaturan | Jalankan sebagai **Administrator**, atau gunakan `CbtKiosk.exe --settings`. |
| Aplikasi tidak ikut menyala saat Windows login | Lihat bagian **G. Startup tidak jalan** di bawah. |
| Aplikasi tetap terkunci & pengawas lupa password | Lihat bagian F. |
| Ikon tray tidak terlihat | Klik panah **▲** di pojok kanan bawah untuk menampilkan ikon tersembunyi. |
| Pintasan tidak berfungsi (F5 / Ctrl+Alt+Q) | Klik dulu di area halaman ujian agar aplikasi menjadi jendela aktif, lalu coba lagi. Pintasan aktif selama aplikasi berjalan. |
| Ingin siswa TIDAK perlu login ulang | Matikan opsi *"Hapus sesi/cookies saat keluar"* di Pengaturan. |

### Melihat log

Log tersimpan di:

```
C:\ProgramData\CbtKiosk\logs\cbtkiosk-YYYYMMDD.log
```

Tombol **Buka folder log** ada di jendela Pengaturan.

---

## G. Startup tidak jalan (sudah disetel tapi tidak muncul saat booting)

Gejala: entri sudah muncul di **Task Manager → Startup**, tetapi aplikasi tidak terbuka setelah
Windows login. Penyebab tersering, berurutan:

1. **File diblokir Windows (Mark of the Web).** `.exe` yang diunduh dari internet ditandai
   "berasal dari internet" dan Windows menolak menjalankannya otomatis (tidak ada yang bisa klik
   *"Run anyway"* saat startup).
   - **Solusi:** klik kanan `CbtKiosk.exe` → **Properties** → centang **Unblock** → OK.
   - Sejak v1.5.0 aplikasi **mencoba menghapus tanda ini sendiri** setiap kali dibuka.
2. **Path di registry salah / file dipindah.** Jika `CbtKiosk.exe` dipindah setelah setting,
   entri menunjuk file yang tidak ada lagi.
   - **Solusi:** taruh `.exe` di lokasi permanen (mis. `C:\CbtKiosk\`), lalu buka Pengaturan →
     **Simpan** ulang. Sejak v1.5.0 aplikasi **memperbaiki path ini sendiri** saat dijalankan.
3. **Status "Disabled" di Task Manager.** Buka Task Manager → **Startup** → klik kanan entri →
   **Enable**.
4. **Kebijakan grup (Group Policy) menonaktifkan Run key.** Umum di komputer sekolah/managed.
   - **Solusi:** gunakan metode **Scheduled Task** (lihat langkah di bawah).
5. **Cakupan "semua pengguna" tanpa hak Administrator** sehingga entri tidak pernah ditulis.

### Langkah diagnosa cepat

1. Buka **Pengaturan** (`CbtKiosk.exe --settings`) → bagian *Saat Windows menyala (startup)*.
2. Klik tombol **"Periksa startup"**. Akan muncul laporan: path aplikasi, isi registry Run,
   apakah file di path itu benar-benar ada, dan status scheduled task.
3. Ikuti hasilnya:
   - *"file di path itu ada: TIDAK"* → path salah; **Simpan** ulang dari lokasi file yang benar.
   - *"Registry Run: TIDAK ada"* → centang opsi lalu **Simpan** (sebagai Administrator bila "semua pengguna").
   - Bila registry sudah benar tetapi tetap tidak jalan → coba **Scheduled Task**.

### Ganti ke metode Scheduled Task (lebih andal)

Di **Pengaturan** → *Metode startup* → pilih **"Scheduled Task (lebih andal)"** → **Simpan**.
Aplikasi akan membuat task Windows bernama `CbtKiosk` dengan pemicu **"At logon"**. Cek hasilnya
di **Task Scheduler** (`taskschd.msc`) → *Task Scheduler Library* → `CbtKiosk`.

> Metode Scheduled Task juga bekerja pada kebijakan yang memblokir Run key, dan bisa dibuat
> berjalan dengan hak tertinggi.

### Cara uji tanpa reboot

Jalankan `schtasks /Run /TN CbtKiosk` (metode task) — atau — log off lalu login kembali.

---

## F. Darurat: pengawas terkunci & tidak tahu password

Bila benar-benar darurat (server mati, password cadangan lupa):

1. **Log off / restart** komputer siswa (tombol power). Aplikasi tidak mengubah sistem secara
   permanen, jadi setelah login ulang desktop normal.
   - *Catatan:* bila memakai **Mode Kios Kuat (bagian D)**, komputer akan kembali membuka aplikasi
     ini saat login. Untuk menghentikannya sementara, masuk dengan **akun Administrator** dan
     kembalikan shell (bagian D.3).
2. Buka Pengaturan sebagai Administrator dan atur ulang password.

> Jika `DisableTaskMgr` sempat diaktifkan aplikasi lalu komputer dimatikan paksa, nilai tersebut
> bisa tetap tersisa untuk pengguna itu. Kembalikan dengan menjalankan Pengaturan sebagai
> Administrator lalu keluar normal, atau:
> ```cmd
> reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v DisableTaskMgr /t REG_DWORD /d 0 /f
> ```
