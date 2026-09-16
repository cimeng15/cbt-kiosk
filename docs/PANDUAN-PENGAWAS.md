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

1. Klik **tab menu** (☰) yang melayang di **tepi kanan layar**.
2. Menu akan terbuka ke arah kiri. Klik **"Keluar dari ujian"**.
   - Untuk memuat ulang halaman ujian, klik **"Muat ulang"**.
3. Masukkan **password** (password yang dikelola di panel CBT).
4. Aplikasi menutup dan mengembalikan desktop.

> Alternatif: klik kanan ikon **CBT Kiosk** di **system tray** (pojok kanan bawah, mungkin
> tersembunyi di panah ▲) → **Keluar dari ujian…**.

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
| Aplikasi tetap terkunci & pengawas lupa password | Lihat bagian F. |
| Ikon tray tidak terlihat | Klik panah **▲** di pojok kanan bawah untuk menampilkan ikon tersembunyi. |
| Tab menu (☰) di tepi kanan tidak terlihat | Pastikan aplikasi berjalan (bukan hanya ikon tray). Tab menempel di tepi kanan tengah layar; pada monitor kedua pindahkan jendela ke monitor utama. |
| Ingin siswa TIDAK perlu login ulang | Matikan opsi *"Hapus sesi/cookies saat keluar"* di Pengaturan. |

### Melihat log

Log tersimpan di:

```
C:\ProgramData\CbtKiosk\logs\cbtkiosk-YYYYMMDD.log
```

Tombol **Buka folder log** ada di jendela Pengaturan.

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
