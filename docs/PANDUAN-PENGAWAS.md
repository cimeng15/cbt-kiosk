# Panduan Pengawas — CBT Kiosk

Panduan singkat untuk pengawas/guru yang mengawasi ujian.

---

## A. Sebelum ujian (persiapan)

> **Dua berkas saja:** `CbtKiosk.exe` (aplikasi ujian, ± 63 MB) dan `CbtKioskTool.exe`
> (aplikasi pengaturan, **± 50 KB**). Simpan keduanya di folder yang sama, mis. `C:\CbtKiosk\`.

1. **Pastikan WebView2 terpasang.** Di komputer siswa, buka salah satu:
   - *Settings → Apps* dan cari "WebView2", atau
   - jalankan `CbtKiosk.exe`. Bila muncul pesan "Tidak dapat memulai komponen browser",
     pasang WebView2 Runtime dari <https://go.microsoft.com/fwlink/p/?LinkId=2124703>.

2. **Atur aplikasi (sekali saja, sebagai Administrator).**
   - Klik kanan **`CbtKioskTool.exe`** → *Run as administrator* (aplikasi pengaturan ringan,
     tidak perlu console; ukurannya hanya ± 50 KB).
   - Periksa **URL ujian** dan **Endpoint password**, lalu klik **Tes koneksi** → harus muncul
     "OK — endpoint terjangkau".
   - Bagian **Password darurat (offline)**: ganti password bawaan (lihat bagian C), dan isi
     **Password pengaturan** bila ingin siswa tidak bisa membuka menu Pengaturan.
   - Bagian **Startup Windows dan aplikasi kiosk**: tentukan lokasi `CbtKiosk.exe`, atur
     auto-start bila perlu.
   - Klik **Simpan**.

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
>
> Pada pesan *"Password sudah kedaluwarsa"* dan *"server tidak tersedia"* aplikasi **mengingatkan
> bahwa password darurat offline tetap bisa dipakai** — masukkan password darurat (bagian C).

> **Sesi dihapus saat keluar.** Cookies dan data situs dibersihkan, sehingga saat aplikasi
> dibuka lagi siswa **harus login ulang**. Ini diatur oleh opsi *"Hapus sesi/cookies saat keluar"*
> di Pengaturan (aktif secara bawaan).

---

## C. Password darurat offline (internet/server mati ATAU password kedaluwarsa)

Ini **jaring pengaman** agar pengawas tidak pernah terkunci di luar mode ujian. Password darurat
disimpan di komputer dan **selalu diterima** untuk keluar:

- internet / server CBT mati total, **atau**
- password online dari panel CBT sudah **kedaluwarsa** (muncul pesan *"Password sudah kedaluwarsa"*), **atau**
- server hidup tetapi password online tidak bisa dipakai — password darurat tetap sebagai jalan terakhir.

### Password darurat BAWAAN (sudah aktif sejak awal)

Sejak **v1.7.0** setiap komputer sudah membawa password darurat bawaan, jadi komputer baru pun tidak
bisa terkunci gara-gara belum diatur:

```
smkdata2026
```

> **Penting:** password bawaan ini **berhenti berlaku otomatis** begitu pengawas menyimpan password
> darurat sendiri. Jadi isi password sendiri di setiap lab, karena `smkdata2026` tercantum di
> dokumentasi publik.

### Mengatur / mengganti password darurat

1. Buka **aplikasi pengaturan**: klik kanan `CbtKioskTool.exe` → *Run as administrator*.
   (Alternatif lama: `CbtKiosk.exe --settings`.)
2. Bagian **"Password darurat (offline)"**:
   - Kolom *Password darurat baru* + *Ulangi password darurat* → isi password darurat Anda.
   - Biarkan **kosong** bila ingin tetap memakai bawaan `smkdata2026`.
   - Hilangkan centang *"Pakai password darurat BAWAAN…"* bila kiosk **hanya** boleh dibuka dengan
     password dari panel CBT (tidak disarankan: bila server mati, komputer tidak bisa dibuka).
3. Klik **Simpan**. Status di atas kolom akan menunjukkan password mana yang sedang berlaku.
   - Password disimpan sebagai **hash SHA-256** (tidak dalam bentuk teks biasa).
4. Beri tahu password ini **hanya kepada pengawas** yang berwenang.

### Memeriksa / menghapus

- Status di tab *Keamanan & Penguncian* selalu menampilkan apakah yang berlaku password **sendiri**,
  **bawaan pabrik**, atau **tidak ada**.
- Tombol **Hapus** mengembalikan ke bawaan pabrik (bukan menghapus jaring pengaman).

> **Keamanan:** jaga kerahasiaan password darurat. Siapa pun yang tahu password ini bisa keluar
> dari mode ujian meski server mati. Ganti `smkdata2026` sebelum ujian pertama.

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
| "Password sudah kedaluwarsa" | Perbarui password di panel CBT (menu pengaturan kios) — **atau** langsung gunakan **password darurat offline** (bagian C), yang tetap berlaku. |
| "Tidak dapat menghubungi server CBT…" | Internet/server mati dan **password darurat belum pernah diatur** serta bawaan pabrik dimatikan. Pakai bawaan `smkdata2026` atau atur lewat Pengaturan (bagian C). |
| Tidak bisa menyimpan Pengaturan | Jalankan **`CbtKioskTool.exe`** sebagai **Administrator** (klik kanan → *Run as administrator*), supaya pengaturan bersama `%ProgramData%\CbtKiosk` dapat ditulis. |
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

1. Buka **aplikasi pengaturan** (`CbtKioskTool.exe`, sebagai Administrator) → bagian
   *Startup Windows dan aplikasi kiosk*.
2. Klik tombol **"Periksa startup"**. Akan muncul laporan: path aplikasi, isi registry Run,
   apakah file di path itu benar-benar ada, dan status scheduled task.
3. Ikuti hasilnya:
   - *"file di path itu ada: TIDAK"* → path salah; **Simpan** ulang dari lokasi file yang benar.
   - *"Registry Run: TIDAK ada"* → centang opsi lalu **Simpan** (sebagai Administrator bila "semua pengguna").
   - Bila registry sudah benar tetapi tetap tidak jalan → coba **Scheduled Task**.

### Ganti ke metode Scheduled Task (lebih andal)

Di aplikasi pengaturan → bagian *Startup Windows dan aplikasi kiosk* → *Metode* → pilih
**"Scheduled Task (lebih andal)"** → **Simpan**.
Aplikasi akan membuat task Windows bernama `CbtKiosk` dengan pemicu **"At logon"**. Cek hasilnya
di **Task Scheduler** (`taskschd.msc`) → *Task Scheduler Library* → `CbtKiosk`.

> Metode Scheduled Task juga bekerja pada kebijakan yang memblokir Run key, dan bisa dibuat
> berjalan dengan hak tertinggi.

### Cara uji tanpa reboot

Jalankan `schtasks /Run /TN CbtKiosk` (metode task) — atau — log off lalu login kembali.

---

## F. Darurat: pengawas terkunci & tidak tahu password

Bila benar-benar darurat (server mati, password darurat lupa):

1. **Log off / restart** komputer siswa (tombol power). Aplikasi tidak mengubah sistem secara
   permanen, jadi setelah login ulang desktop normal.
   - *Catatan:* bila memakai **Mode Kios Kuat (bagian D)**, komputer akan kembali membuka aplikasi
     ini saat login. Untuk menghentikannya sementara, masuk dengan **akun Administrator** dan
     kembalikan shell (bagian D.3).
2. Buka Pengaturan sebagai Administrator dan atur ulang password.

> **Bantuan cepat:** coba dulu password darurat **bawaan pabrik** `smkdata2026` — selama pengawas
> belum pernah menggantinya, password ini masih berlaku (bagian C).
>
> Jika `DisableTaskMgr` sempat diaktifkan aplikasi lalu komputer dimatikan paksa, nilai tersebut
> bisa tetap tersisa untuk pengguna itu. Kembalikan dengan menjalankan Pengaturan sebagai
> Administrator lalu keluar normal, atau:
> ```cmd
> reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v DisableTaskMgr /t REG_DWORD /d 0 /f
> ```
