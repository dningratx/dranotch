<div align="center">

# 🎵 DRANOTCH

### *Aesthetic Dynamic Island & Floating Productivity Companion for Windows*

[![Latest Release](https://img.shields.io/github/v/release/dningratx/dranotch?style=for-the-badge&color=8A2BE2&logo=github)](https://github.com/dningratx/dranotch/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=for-the-badge&logo=windows)](https://github.com/dningratx/dranotch)
[![.NET](https://img.shields.io/badge/.NET-10.0%20WPF-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

<br/>

**DRANOTCH** menghadirkan pengalaman *Dynamic Island* modern, ramping, dan elegan langsung di bagian atas desktop Windows Anda.  
Menggabungkan **pemutar musik pintar**, **lirik sinkronisasi otomatis (Karaoke)**, dan **To-Do List produktif yang dapat dilepas (detachable)** dengan sinkronisasi Google Drive.

[📥 **Unduh Versi Terbaru (v1.2.1)**](https://github.com/dningratx/dranotch/releases/latest) • [✨ Fitur Utama](#-fitur-utama) • [⌨️ Shortcut](#️-pintasan-keyboard-hotkeys) • [🛠️ Panduan Build](#️-cara-build-dari-source)

</div>

---

## ✨ Fitur Utama

### 🎧 1. Dynamic Notch & Smart Media Player
- **Island Mode Pintar**: Notch melayang di tepi atas layar yang otomatis melebar saat didekati (*Expand on Hover*) dan mengecil saat idle (*Compact Mode*).
- **Integrasi Windows Media Session**: Mendeteksi otomatis lagu yang sedang diputar dari **Spotify**, **YouTube**, **Apple Music**, browser (Chrome/Edge/Firefox), atau pemutar lokal.
- **Built-in YouTube Music**: Dilengkapi pemutar bawaan berbasis WebView2 dengan penyimpanan sesi akun Google permanen.

### 🎤 2. Lirik Real-Time & Mode Karaoke
- **Auto-Search LRCLIB**: Pencarian otomatis lirik tersinkronisasi (*timed lyrics*) langsung saat lagu berganti.
- **Mode Layar Penuh Lirik**: Tampilan lirik transparan (*glassmorphism*) dengan kustomisasi ukuran font dan transparansi.
- **Click-Through Mode**: Buat panel lirik tembus klik sehingga tidak menghalangi pekerjaan atau game Anda.
- **Manual Offset Sync**: Atur offset waktu lirik (+0.5s / -0.5s) jika lirik terlalu cepat atau terlambat.
- **Impor Lirik Lokal**: Dukungan file `.lrc` dan `.txt` lokal.

### 📋 3. To-Do List Fleksibel & Mandiri *(Baru di v1.2.0)*
- **Jendela Melayang Mandiri (*Detachable*)**: Lepas To-Do list dari panel musik dengan sekali klik tombol **`⧉ Lepas`**. Letakkan di mana saja atau di monitor kedua.
- **Docking Mudah**: Kembalikan To-Do list menempel ke musik kapan saja dengan tombol **`⇲ Pasang ke Musik`**.
- **Ticker Berjalan (*Live Ticker*)**: Pantau tugas aktif langsung dari mode compact tanpa membuka jendela besar.
- **Kategori & Tag**: Kelompokkan tugas dengan tag (misal: `#KERJA`, `#KULIAH`, `#PRIBADI`).
- **Filter Cepat**: Tab filter instan (*Semua*, *Aktif*, *Selesai*) dan pembersihan tugas selesai sekali klik.
- **Google Drive Cloud Sync**: Sinkronisasi daftar tugas ke cloud akun Google Drive pribadi Anda secara aman.

### 🎛️ 4. Pilihan Komponen Tampilan *(Baru di v1.2.0)*
Sesuaikan apa yang ingin Anda tampilkan melalui menu Pengaturan:
- 🎶 **Musik & To-Do List (Keduanya)**: Tampilan lengkap pemutar musik dan manajemen tugas.
- 🎵 **Hanya Pemutar Musik**: Tampilan murni pemutar musik tanpa elemen To-Do.
- ✅ **Hanya To-Do List**: Transformasi DRANOTCH menjadi widget produktivitas murni dengan notch ringkas dan ticker tugas.

---

## 📸 Tampilan Antarmuka

| Mode | Deskripsi |
| :--- | :--- |
| **Compact Notch** | Menampilkan artwork album, status lagu/lirik berjalan, ticker tugas aktif, dan tombol putar/jeda. |
| **Expanded Panel** | Panel gabungan kontrol musik lengkap, progress seekbar, lirik, dan daftar tugas To-Do. |
| **Detached To-Do** | Jendela To-Do mandiri dengan tema gelap elegan, dukungan drag & drop bebas, dan auto-save posisi. |
| **Lyrics Overlay** | Tampilan lirik berukuran besar dengan scrolling halus otomatis mengikuti vokal lagu. |

---

## ⌨️ Pintasan Keyboard (Hotkeys)

| Shortcut | Aksi |
| :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>N</kbd> | Tampilkan / Sembunyikan panel DRANOTCH |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>L</kbd> | Buka / Tutup Mode Lirik (*Lyrics Mode*) |
| <kbd>Enter</kbd> | Tambah tugas baru saat mengetik di input To-Do |

*Pintasan keyboard dapat disesuaikan melalui menu **Pengaturan (`⚙`)**.*

---

## 📥 Pemasangan (Instalasi)

1. Unduh installer setup terbaru: [**`DRANOTCH-Setup-1.2.1.exe`**](https://github.com/dningratx/dranotch/releases/latest).
2. Jalankan berkas installer dan ikuti petunjuk pemasangan.
3. DRANOTCH akan muncul di bagian atas layar desktop Anda.

> **Catatan Sistem**: Memerlukan Windows 10 (versi 1809 atau lebih baru) atau Windows 11 (64-bit).

---

## 🛠️ Cara Build dari Source

### Prasyarat
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) *(opsional, untuk mengkompilasi file installer)*

### Langkah Build

1. Clone repositori:
   ```bash
   git clone https://github.com/dningratx/dranotch.git
   cd dranotch
   ```

2. Jalankan pengujian unit:
   ```bash
   dotnet test
   ```

3. Jalankan aplikasi langsung dalam mode Debug:
   ```bash
   dotnet run --project src/NotchMusic/NotchMusic.csproj
   ```

4. Build release portabel dan installer otomatis:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
   ```
   *Hasil biner portabel akan berada di `dist/Publish/` dan paket installer di `dist/Installer/`.*

---

## 💡 Teknologi yang Digunakan

- **Framework**: [.NET 10](https://dotnet.microsoft.com/) & WPF (Windows Presentation Foundation)
- **Web Runtime**: [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (untuk pemutar YouTube Music)
- **Media API**: Windows System Media Transport Controls (`Windows.Media.Control`)
- **Lirik API**: [LRCLIB API](https://lrclib.net/)
- **Cloud Sync**: [Google Drive API v3](https://developers.google.com/drive)
- **Packaging**: [Inno Setup Compiler](https://jrsoftware.org/isdl.php)

---

## 📄 Lisensi

Proyek ini dilisensikan di bawah lisensi [MIT License](LICENSE).

<div align="center">
Dibuat dengan ❤️ untuk meningkatkan estetika dan produktivitas desktop Windows Anda.
</div>
