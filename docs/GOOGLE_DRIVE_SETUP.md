# 🔐 Panduan Konfigurasi Google Drive API untuk DRANOTCH

Dokumen ini menjelaskan langkah-langkah membuat kredensial Google OAuth 2.0 Client pribadi secara gratis untuk fitur sinkronisasi To-Do List di **DRANOTCH**.

---

## 📌 Mengapa Menggunakan Kredensial Sendiri?
1. **Keamanan & Privasi**: Kontrol penuh atas akses Google Cloud Project Anda sendiri.
2. **Bebas Limit Kuota**: Tidak terpengaruh kuota request pengguna DRANOTCH lainnya.
3. **Standar Open-Source**: Memenuhi *best practice* keamanan repositori publik.

> [!NOTE]
> DRANOTCH hanya meminta hak akses ke scope `https://www.googleapis.com/auth/drive.file`.
> Artinya, aplikasi ini **HANYA** dapat membaca dan memodifikasi file yang dibuatnya sendiri (`todos.json` di dalam folder `DRANOTCH`), dan **TIDAK BISA** melihat dokumen, foto, atau file lain di Google Drive Anda.

---

## 🚀 Langkah-Langkah Pembuatan Kredensial

### Langkah 1: Buat Proyek di Google Cloud Console
1. Buka [Google Cloud Console](https://console.cloud.google.com/).
2. Login menggunakan akun Google Anda.
3. Klik dropdown proyek di bagian atas (sebelah logo Google Cloud), lalu klik **New Project** (Proyek Baru).
4. Beri nama proyek, misalnya `DRANOTCH Desktop`, lalu klik **Create**.

---

### Langkah 2: Aktifkan Google Drive API
1. Pada menu sebelah kiri, masuk ke **APIs & Services** > **Enabled APIs & Services**.
2. Klik **+ ENABLE APIS AND SERVICES** di bagian atas.
3. Cari `Google Drive API`, klik hasilnya, lalu klik **Enable** (Aktifkan).

---

### Langkah 3: Konfigurasi Layar Persetujuan OAuth (OAuth Consent Screen)
1. Di menu sebelah kiri, klik **APIs & Services** > **OAuth consent screen**.
2. Pilih User Type: **External** (Eksternal), lalu klik **Create**.
3. Isi informasi dasar aplikasi:
   * **App name**: `DRANOTCH`
   * **User support email**: Pilih email Google Anda.
   * **Developer contact information**: Masukkan email Anda.
4. Klik **Save and Continue** pada langkah *Scopes* (bisa dilewati karena aplikasi meminta scope `drive.file` saat runtime).
5. Pada bagian **Test users**:
   * Klik **+ ADD USERS**.
   * Masukkan alamat email Google yang akan Anda pakai untuk login di DRANOTCH.
   * Klik **Save and Continue**.

---

### Langkah 4: Buat OAuth 2.0 Client ID
1. Di menu sebelah kiri, klik **APIs & Services** > **Credentials**.
2. Klik **+ CREATE CREDENTIALS** di bagian atas, lalu pilih **OAuth client ID**.
3. Pada dropdown **Application type**, pilih **Desktop app** (Aplikasi Desktop).
4. Beri nama, misalnya `DRANOTCH Client`, lalu klik **Create**.
5. Jendela akan menampilkan **Client ID** dan **Client Secret**, serta tombol **DOWNLOAD JSON**.
6. Klik **DOWNLOAD JSON** untuk mengunduh berkas `client_secret_...json`.

---

## 📥 Cara Memasukkan Kredensial ke DRANOTCH

Pilih salah satu dari 3 cara praktis berikut:

### Opsi A: Lewat Antarmuka Aplikasi (Paling Mudah)
1. Buka aplikasi **DRANOTCH**.
2. Buka menu **Pengaturan** (ikon gear / roda gigi di notch).
3. Gulir ke bagian **Sinkronisasi Google Drive**.
4. Klik tombol **"Impor File credentials.json"**, lalu pilih file JSON yang baru Anda unduh.
5. Selesai! Klik tombol **"Login Google Drive"** untuk menghubungkan akun Anda.

### Opsi B: Letakkan Berkas di Folder Aplikasi
1. Ganti nama file JSON yang diunduh menjadi `credentials.json`.
2. Letakkan file tersebut di:
   `%LOCALAPPDATA%\DRANOTCH\credentials.json`
3. Jalankan aplikasi DRANOTCH, aplikasi akan mendeteksi dan mengimpor kredensial secara otomatis saat startup.

### Opsi C: Menggunakan Environment Variables (Developer / CI)
Setel variabel lingkungan di sistem Anda atau salin `.env.example` menjadi `.env`:
```bash
GOOGLE_DRIVE_CLIENT_ID=isi_client_id_anda.apps.googleusercontent.com
GOOGLE_DRIVE_CLIENT_SECRET=isi_client_secret_anda
```

---

## ❓ FAQ & Troubleshooting

* **Peringatan *"Google hasn't verified this app"***:  
  Karena aplikasi ini berstatus *Testing* dan menggunakan Client ID milik Anda sendiri, Google akan menampilkan peringatan keamanan saat pertama kali login di browser. Klik **Advanced** (Lanjutan) > **Go to DRANOTCH (unsafe)**. Ini 100% aman karena Anda adalah pemilik project tersebut.
* **Ingin Memutuskan Akun?**  
  Buka Pengaturan DRANOTCH > klik **"Putuskan Sambungan"**. Anda juga bisa menghapus otorisasi aplikasi dari menu [Keamanan Akun Google](https://myaccount.google.com/permissions).
