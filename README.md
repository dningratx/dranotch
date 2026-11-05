<div align="center">

# 🎵 DRANOTCH

### *Aesthetic Dynamic Island & Floating Productivity Companion for Windows*

[![Latest Release](https://img.shields.io/github/v/release/dningratx/dranotch?style=for-the-badge&color=8A2BE2&logo=github)](https://github.com/dningratx/dranotch/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=for-the-badge&logo=windows)](https://github.com/dningratx/dranotch)
[![.NET](https://img.shields.io/badge/.NET-10.0%20WPF-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

<br/>

**DRANOTCH** menghadirkan pengalaman *Dynamic Island* modern, ramping, dan elegan langsung di bagian atas desktop Windows Anda.  
Menggabungkan **pemutar musik pintar (YouTube Music & Spotify bawaan)**, **Live TV Indonesia & Dunia**, **Radio Online Indonesia & Dunia**, **Cuaca & Gempa Bumi BMKG (Presisi Kelurahan/Desa)**, **Pengingat Pintar (Minum Air, Minum Obat, Istirahat Mata & Aktivitas)**, **Catatan Tempel (Sticky Notes melayang dengan Google Drive Sync)**, **lirik sinkronisasi otomatis (Karaoke)**, **To-Do List produktif yang dapat dilepas (detachable)** dengan sinkronisasi Google Drive, **jadwal waktu sholat & pengingat adzan otomatis**, serta **Browser Mengambang**.

[📥 **Unduh Versi Terbaru (v1.9.0)**](https://github.com/dningratx/dranotch/releases/latest) • [🌐 **Halaman Rilis Resmi**](https://dningratx.github.io/dranotch/) • [✨ Fitur Utama](#-fitur-utama) • [⌨️ Shortcut](#️-pintasan-keyboard-hotkeys) • [🛠️ Panduan Build](#️-cara-build-dari-source)

</div>

---

## ✨ Fitur Utama

### 🎧 1. Dynamic Notch & Smart Media Player
- **Island Mode Pintar**: Notch melayang di tepi atas layar yang otomatis melebar saat didekati (*Expand on Hover*) dan mengecil saat idle (*Compact Mode*).
- **Integrasi Windows Media Session**: Mendeteksi otomatis lagu yang sedang diputar dari **Spotify**, **YouTube**, **Apple Music**, browser (Chrome/Edge/Firefox), atau pemutar lokal.
- **Built-in YouTube Music**: Dilengkapi pemutar bawaan berbasis WebView2 dengan penyimpanan sesi akun Google permanen, dukungan bebas ubah ukuran (*free resizing*), pilihan ukuran cepat preset (Mini PiP, Ringkas, Sedang, Standar, HD), zoom factor, dan mode pin mengambang (*Always On Top*).

### 🎤 2. Lirik Real-Time & Mode Karaoke
- **Auto-Search LRCLIB**: Pencarian otomatis lirik tersinkronisasi (*timed lyrics*) langsung saat lagu berganti.
- **Mode Layar Penuh Lirik**: Tampilan panggung lirik karaoke mewah (*Luxury Stage Glass*) berukuran luas (`640×400`) dengan kustomisasi ukuran font dan transparansi.
- **Manual Offset Sync**: Atur offset waktu lirik (+0.5s / -0.5s) jika lirik terlalu cepat atau terlambat.
- **Impor Lirik Lokal**: Dukungan file `.lrc` dan `.txt` lokal.

### 📋 3. To-Do List Fleksibel & Mandiri *(Baru di v1.2.0)*
- **Jendela Melayang Mandiri (*Detachable*)**: Lepas To-Do list dari panel musik dengan sekali klik tombol **`⧉ Lepas`**. Letakkan di mana saja atau di monitor kedua.
- **Docking Mudah**: Kembalikan To-Do list menempel ke musik kapan saja dengan tombol **`⇲ Pasang ke Musik`**.
- **Ticker Berjalan (*Live Ticker*)**: Pantau tugas aktif langsung dari mode compact tanpa membuka jendela besar.
- **Kategori & Tag**: Kelompokkan tugas dengan tag (misal: `#KERJA`, `#KULIAH`, `#PRIBADI`).
- **Geser & Pindah Kategori *(Baru di v1.7.0)***: Atur urutan kategori ke atas atau ke bawah secara fleksibel menggunakan tombol panah (`▲`/`▼`) maupun seret & lepas (*drag and drop* `⋮⋮`). Seluruh daftar tugas di dalam kategori (*anak-anaknya*) otomatis ikut berpindah secara utuh dengan urutan yang tetap terjaga.
- **Filter Cepat**: Tab filter instan (*Semua*, *Aktif*, *Selesai*) dan pembersihan tugas selesai sekali klik.
- **Google Drive Cloud Sync**: Sinkronisasi daftar tugas ke cloud akun Google Drive pribadi Anda secara aman. *(Panduan setup OAuth pribadi: [docs/GOOGLE_DRIVE_SETUP.md](docs/GOOGLE_DRIVE_SETUP.md))*

### 🎛️ 4. Pilihan Komponen Tampilan *(Baru di v1.2.0)*
Sesuaikan apa yang ingin Anda tampilkan melalui menu Pengaturan:
- 🎶 **Musik & To-Do List (Keduanya)**: Tampilan lengkap pemutar musik dan manajemen tugas.
- 🎵 **Hanya Pemutar Musik**: Tampilan murni pemutar musik tanpa elemen To-Do.
- ✅ **Hanya To-Do List**: Transformasi DRANOTCH menjadi widget produktivitas murni dengan notch ringkas dan ticker tugas.

### 🖤 5. Tampilan Estetik OLED Monokrom & Kontrol Mandiri *(Baru di v1.2.1)*
- **Deep Black Palette**: Desain antarmuka bernuansa hitam pekat (*true dark / OLED*) yang ramping, ramah di mata untuk penggunaan malam hari, dan hemat daya.
- **Kontras Tinggi & Bersih**: Aksen monokrom putih dan abu-abu terang yang tajam pada tombol kontrol, teks, dan status lagu.
- **Tombol Minimize Mandiri**: Saat mode To-Do List terpisah (*detached*), tombol minimize (`▴`) tetap tersedia secara independen di kedua jendela (Musik dan To-Do) untuk fleksibilitas maksimal.

### 🕌 6. Waktu Sholat & Pengingat Otomatis *(Baru di v1.3.0)*
- **Jadwal Sholat Presisi & Kalender Hijriah**: Perhitungan astronomis offline presisi standar Kemenag RI / MABIMS untuk Imsak, Subuh, Terbit, Dzuhur, Ashar, Maghrib, dan Isya.
- **Pilihan 40+ Kota & Deteksi Lokasi (IP)**: Pilihan kota-kota besar di seluruh Indonesia (WIB, WITA, WIT) serta tombol deteksi otomatis koordinat lokasi via IP.
- **Pengingat Sebelum Masuk Waktu**: Peringatan bersiap (5, 10, 15, 20, atau 30 menit sebelumnya) untuk mengambil wudhu dan menuju masjid.
- **Pengingat Tepat Waktu (Adzan) & Jeda Musik Otomatis**: Kartu notifikasi melayang di Dynamic Island, notifikasi Windows, nada dering bel chime yang tenang, dan fitur jeda musik otomatis (*auto-pause*) saat adzan berkumandang.
- **Pengingat Sesudah Sholat**: Notifikasi konfirmasi ramah (*"Sudahkah Anda sholat?"*) setelah waktu sholat berlalu agar ibadah tidak terlewat.
- **Pill Ringkas di Compact Notch & Kartu Lengkap**: Pantau hitung mundur sholat berikutnya langsung dari notch (`🕌 Dzuhur 11:39 (-15m)`) dan buka jadwal harian lengkap kapan saja dengan sekali klik tombol **`🕌 Sholat`**.

### 🧩 7. Desain Modular 3-Komponen, Drag & Drop To-Do, & Keamanan Terpercaya *(v1.4.0)*
- **Urutan Modular Terintegrasi**: Dynamic Notch default menempel di tengah atas layar dengan 3 bagian rapi bergaris pemisah modern:
  `[ Pemutar Musik ]` ╎ `[ Waktu Sholat ]` ╎ `[ To-Do List ]`
- **Waktu Sholat Dapat Dilepas (*Detachable*)**: Komponen waktu sholat kini bisa dilepas menjadi jendela melayang mandiri (`PrayerWindow`) atau dipasang kembali ke Dynamic Notch kapan saja.
- **Drag & Drop Urutan To-Do**: Susun prioritas tugas secara visual cukup dengan menarik ikon `⋮⋮` ke atas atau ke bawah. Perubahan urutan langsung tersimpan dan otomatis tersinkronisasi ke Google Drive.
- **Continuous Live Ticker**: Teks berjalan di mode compact mengalir lancar dari kanan ke kiri tanpa jeda pergantian kategori, lengkap dengan jeda pintar saat kursor diarahkan (*pause on hover*).
- **Akses Cepat Keluar / Tutup Aplikasi**: Tombol `✕` di header notch (dengan efek hover merah elegan), opsi *Tutup Aplikasi* pada klik kanan notch capsule, dan tombol keluar di pengaturan.
- **Digital Code Signing & 1-Click Trusted Certificate**: Installer dan binary ditandatangani secara digital dengan SHA-256 dan stempel waktu resmi DigiCert, disertai script `Install-Certificate.bat` untuk menghilangkan peringatan Windows SmartScreen.

### 🚀 8. Pembaruan Fitur & Visual *(v1.4.1)*
- **Ikon Baru & Branding Segar**: Ikon aplikasi resmi baru yang tajam dan modern dengan multi-resolusi (16px hingga 256px), tampil sempurna di taskbar, installer setup, system tray, dan control panel uninstaller.
- **Integrasi YouTube Music Pintar**: Tombol *Buka YouTube Music* tersemat rapi di area lirik saat tidak ada musik yang aktif. Saat diklik, notch otomatis mengecil (*compact*) dan jendela YouTube Music muncul tepat di tengah bawah layar tanpa melebihi batas layar monitor.
- **Status Google Drive Langsung & Sinkronisasi Instan**: Tombol status Google Drive di header To-Do (`G-Drive Terhubung` / `Sambungkan G-Drive`). Klik untuk langsung login atau sinkronisasi. Setiap penambahan, pengeditan teks, atau penghapusan tugas otomatis tersinkronisasi ke Google Drive.
- **Edit Tugas di Tempat (*Inline To-Do Editing*)**: Klik dua kali pada nama tugas (atau klik ikon pensil) untuk mengedit teks secara langsung. Tekan <kbd>Enter</kbd> untuk menyimpan perubahan atau <kbd>Esc</kbd> untuk membatalkan.
- **Intelligent Hover Delay**: Peningkatan kenyamanan dengan jeda sengaja (650ms) saat mouse mendekati notch agar panel tidak terbuka secara tidak sengaja ketika kursor hanya melintas cepat.

### 🌟 9. Logo DRANOTCH Terintegrasi & Waktu Sholat di Expanded Mode *(v1.4.2)*
- **Logo Resmi DRANOTCH di Notch Capsule**: Menggantikan placeholder ikon musik `♪` dengan lambang resmi DRANOTCH saat notch dalam kondisi idle / standby, tampil estetik dan serasi dengan desain kapsul.
- **Jadwal Waktu Sholat Terintegrasi di Expanded Mode**: Saat panel diperluas (*expanded*), jadwal waktu sholat kini tampil berdampingan secara otomatis di antara pemutar musik dan To-Do list, lengkap dengan kartu waktu sholat aktif, hitung mundur real-time, dan tombol lepas (*detach*).
- **Pembaruan Ikon Desktop Otomatis**: Menjamin ikon shortcut desktop dan start menu langsung diperbarui ke desain ikon terbaru saat instalasi maupun saat update otomatis (*in-app update*), dengan pembersihan cache ikon Windows Shell secara instan.

### 🔄 10. Auto-Sync Realtime Lintas Komputer & Desain Tombol Minimalis *(v1.4.3)*
- **Sinkronisasi Otomatis Realtime Antar Komputer**: Jika DRANOTCH terpasang di 2 atau lebih komputer dengan akun Google Drive yang sama, perubahan tugas (tambah, edit, centang selesai, atau hapus) di salah satu komputer akan otomatis terupdate di komputer lain dalam hitungan detik tanpa harus menekan tombol sinkronisasi.
- **Deteksi Cerdas Metadata Cloud**: Memeriksa perubahan timestamp berkas cloud di latar belakang secara hemat daya dan hemat kuota tanpa mengunduh ulang jika tidak ada perubahan.
- **Desain Tombol Pasang & Lepas Bersih**: Tombol Pasang (`⇲`) dan Lepas (`⧉`) kini tampil minimalis hanya dengan lambang ikon tanpa teks label panjang, membuat header jendela To-Do, Waktu Sholat, dan Notch terlihat jauh lebih rapi dan ringkas.

### 🍱 11. Manajer Komponen Simbolik, Jendela Lepas Pas, & UI Anti-Terpotong *(v1.4.4)*
- **Tombol Komponen Bersimbol Dinamis Tanpa Teks**: Tombol pengelola komponen di header notch kini tampil ultra-ramping hanya berupa simbol ikonik tanpa label teks yang memakan tempat. Menampilkan simbol dinamis (`🕌 ✓`, `🕌`, atau `✓`) saat ada komponen yang ditutup, memungkinkan pemulihan instan ke Notch atau jendela mengambang dalam 1 klik.
- **Tutup Jendela Lepas Mandiri (*Decoupled Close*)**: Menutup jendela mengambang Waktu Sholat atau To-Do List dengan tombol `✕` tidak lagi memaksanya kembali menempel ke notch. Jendela tetap dalam status terlepas/tertutup hingga Anda memutuskan untuk memasangnya kembali melalui tombol pasang (`⇲`).
- **Jendela Waktu Sholat Pas Vertikal (*Shrink-Wrap Fit*)**: Tampilan jendela lepas Waktu Sholat kini otomatis menyesuaikan tinggi konten secara presisi (*SizeToContent Height*), menghilangkan ruang kosong hitam di bagian bawah.
- **Tata Letak Bebas Terpotong (*Zero-Clipping UI Guarantee*)**: Seluruh header dan tombol aksi kini menggunakan arsitektur `DockPanel` kanan yang terlindungi dari kelengkungan sudut notch (*corner radius*), memastikan tombol `✕`, tombol tambah `+`, dan ikon opsi tidak pernah terpotong.
- **Desain Notch Simetris & Ikon Seragam**: Menghilangkan kekosongan ruang samping notch dengan proporsi simetris (1100px saat rapat) dan menyamakan ukuran ikon DRANOTCH saat expanded dengan mode minimize.
- **Tombol Cepat YT Music & Scrollbar OLED**: Tombol `▶ YT Music` di barisan header atas untuk kemudahan mengganti lagu, serta scrollbar vertikal & horizontal kustom yang menyatu dengan tema gelap OLED.

### 🌐 12. Komponen ke-4: Browser Mengambang & Pemutar Streaming *(v1.5.0)*
- **4 Komponen Layar Modular**: DRANOTCH kini resmi memiliki 4 komponen mandiri yang fleksibel: **Pemutar Musik**, **Waktu Sholat**, **To-Do List**, dan **Browser Mengambang**.
- **Akses Web & Video Streaming Bebas**: Buka YouTube, situs film streaming (LK21), Twitch, Netflix, atau alamat web apa pun langsung dari notch atau menu tray.
- **Windowed Fullscreen**: Mode layar penuh video otomatis dibatasi di dalam jendela mengambang (*in-window fullscreen*), tidak akan menutupi seluruh layar monitor komputer Anda.
- **Navigasi & Drag Selalu Aktif**: Dilengkapi grip geser khusus (`⋮⋮ Geser`) dan batas luar pelindung resize, menjamin jendela selalu bisa dipindahkan dan di-resize bebas kapan pun meski film sedang diputar fullscreen.
- **Preset Ukuran 16:9 Instan (`📐`)**: Pilihan cepat ukuran rasio video standar sekali klik: Mini PiP (360×202), Kecil (480×270), Sedang (720×405), Standar (960×540), hingga HD (1280×720).
- **Mode Mengambang (Always On Top 📌)**: Fitur pin yang membuat jendela video tetap melayang di atas aplikasi lain (coding, game, office, dll).

### 🖥️ 13. Desain Bezel Notch Menyatu ke Atas Layar & UI Browser Minimalis *(v1.5.1)*
- **Siluet Bezel Atas Menyatu (*Top-Mounted Hardware Notch*)**: Desain DRANOTCH kini menempel langsung (*flush*) di tepi paling atas layar Windows (`Top = 0`) dengan lengkungan bahu cekung (*concave fillet ears*) di sisi kiri-kanan atas dan sudut bawah membulat, tampak menyatu secara alami dengan bingkai atas monitor.
- **Auto-Dock & Snap ke Tepi Atas**: Jendela notch otomatis menempel rapat ke tepi atas monitor saat aplikasi dijalankan, saat beralih mode (*Compact* / *Expanded* / *Lyrics*), maupun setelah digeser secara horizontal.
- **Antarmuka Browser Mengambang Lebih Bersih & Ringkas**: Menghapus tombol-tombol pintasan situs di samping kolom URL agar bar pencarian/alamat membentang penuh, menyederhanakan header kiri dengan menyisakan grip `⋮⋮ Geser`, serta merampingkan tombol Pin menjadi hanya ikon `📌`.

### 💎 14. Luxury Mac Dynamic Notch, Ikon Vektor Silver Metallic, & Bar 4-Komponen Terpisah *(v1.5.2)*
- **Desain Luxury MacBook Pro Dynamic Notch**: Ukuran Notch diperbesar secara proporsional (`Height="68"`, `CornerRadius="0,0,28,28"`, bahu lengkung cekung `28×28`, panel *Expanded* `1150px`) dengan bingkai *Specular Rim* (`LuxuryNotchBorderBrush`), permukaan tombol *Satin Pill*, dan tipografi tajam `Segoe UI Variable Display` (*ClearType*).
- **Selalu Presisi di Tengah Atas saat Minimize (`CenterAtTopBezel`)**: Ketika berada di mode *Compact* (minimize), Notch otomatis memposisikan dirinya tepat di tengah-atas layar monitor (`Top = 0`), baik saat pertama dibuka, saat kembali dari mode *Expanded*/*Lyrics*, maupun saat lebar konten dinamis berubah.
- **4 Komponen Terpisah dengan Pembatas di Notch**: Kapsul komponen ke-4 (**`Browser`**) kini ditempatkan langsung di sebelah kanan komponen **To-Do List** lengkap dengan garis pembatas vertikal (`|`), menandakan 4 komponen modular yang terpisah secara jelas:
  `[ Pemutar Musik ]` ╎ `[ Waktu Sholat ]` ╎ `[ To-Do List ]` ╎ `[ Browser ]`
  *(Tombol Browser di dalam Music Player dihapus agar tidak duplikat).*
- **Ikon Vektor Kustom Berwarna Silver Metallic**: Menggantikan emoji bawaan Windows dengan **ikon vektor SVG kustom** berlapis gradien **Silver Metallic** (`#FFFFFF` → `#E2E6F0` → `#949BA8` → `#D4D9E6`) yang serasi dengan logo resmi DRANOTCH di dalam lencana *squircle* `28×28` untuk **Waktu Sholat** (Bulan Sabit & Bintang Berlian), **To-Do List** (Bingkai & Centang Tugas), **Browser** (Bola Dunia Meridian), dan **Musik**.
- **Panggung Lirik Karaoke Lebih Mewah & Bersih**: Memperbesar mode *Lyrics* menjadi `640×400` dengan efek panggung *radial gradient*, kontrol media vektor bertema *Satin Glass*, serta menghapus tombol *Click-Through* agar antarmuka lirik lebih fokus dan elegan.

### 📻 15. Komponen ke-5: Radio Online Indonesia & Dunia, Mode Notch Cerdas (Mini/Idle), & Kunci Pin *(v1.6.0)*
- **5 Komponen Modular Terintegrasi**: Menambahkan komponen **Radio Online** tepat di sebelah kanan **Music Player** dengan urutan barisan:
  `[ Pemutar Musik ]` ╎ `[ Radio Online ]` ╎ `[ Waktu Sholat ]` ╎ `[ To-Do List ]` ╎ `[ Browser ]`
- **Streaming Radio Online Indonesia & Dunia (`RadioWindow`)**:
  - Dilengkapi puluhan stasiun radio populer Indonesia & internasional yang telah diverifikasi (Prambors FM, Hard Rock FM, Gen FM, Jak FM, Trax FM, Most Radio, I-Radio, Cosmopolitan FM, Brava Radio, Elshinta, RRI Pro 1/2/3, Radio Rodja, BBC World Service, Lofi Girl / Chillsky, Jazz24, SomaFM, dll.).
  - **Pencarian Stasiun Radio Dunia Online**: Cari puluhan ribu stasiun radio dari seluruh dunia secara langsung melalui integrasi **Radio-Browser API**.
  - **Mesin Audio WinRT Modern & Auto-Fallback**: Menggunakan `Windows.Media.Playback.MediaPlayer` yang mendukung penuh stream `AAC`, `AAC+`, `MP3`, dan `HLS` dengan sistem *auto-fallback* dan *auto-resolve* otomatis jika suatu kanal stream sedang sibuk.
  - **Stasiun Kustom & Favorit**: Tambahkan URL stream radio favorit Anda sendiri (`+ Tambah Radio`), tandai stasiun favorit (`★`), atur volume radio mandiri, serta perkecil jendela radio ke mode *compact player*.
- **Tombol Mengecilkan Notch (`›‹` / `‹›`) & Mode Idle Cerdas**:
  - Tombol di ujung paling kanan Notch kini berfungsi untuk **mengecilkan atau memperbesar Notch** secara instan.
  - Saat Notch dikecilkan (baik melalui tombol di ujung kanan maupun otomatis saat *idle*), **komponen yang sedang aktif** tetap ditampilkan secara utuh, sedangkan **komponen yang tidak aktif** otomatis dirampingkan menjadi **hanya ikon saja**.
  - **Waktu Sholat Selalu Tampil**: Komponen Waktu Sholat selalu tampil utuh walaupun Notch sedang dalam kondisi *idle* / dikecilkan.
- **Tombol Pin Notch (`📌`) & Kontrol Tanpa Gangguan Hover**:
  - Saat tombol **Pin (`📌`)** diaktifkan, Notch dikunci agar tidak mengecil otomatis ketika *idle*.
  - Menonaktifkan pembukaan panel otomatis saat kursor melintas (*Expand on Hover*) agar tidak mengganggu aktivitas kerja di bagian atas layar.

### ✨ 16. Mode Idle Simetris Berpusat pada Logo DRANOTCH, Jendela Lepas Pemutar Musik, & Transformasi Ikon Pintar *(v1.6.1)*
- **Tampilan Idle Minimalis (Logo DRANOTCH, Waktu Sholat, & Pemutar Musik saat Memutar Lagu)**:
  - Saat kondisi *idle*, notch menampilkan **Logo DRANOTCH**, **Waktu Sholat** tepat di sampingnya (dapat diatur tampil atau disembunyikan melalui Pengaturan), serta **Pemutar Musik** otomatis tetap tampil jika lagu sedang dimainkan (`IsPlaying == true`).
  - Saat kursor diarahkan (*hover*) ke notch, 5 komponen mengembang secara simetris: **Pemutar Musik** & **Radio Online** di sisi kiri, **Browser** & **To-Do List** di sisi kanan, serta **Pengaturan (`⚙`)**, **Waktu Sholat**, & **Tombol Versi** di baris bawah.
- **Mode Pelepasan Pemutar Musik & Lirik (`MusicWindow`)**:
  - Mengklik pemutar musik membuka jendela melayang mandiri yang menampilkan lirik *real-time* 3 baris, *progress bar*, kontrol media & offset satu baris, tombol **`🎤 Karaoke` (Penghilang Vokal *Real-Time* berbasis Web Audio DSP dengan pelestarian Bass)**, tombol **`▶ YT Music`**, tombol minimize (`▴`) yang tetap menampilkan lirik secara ringkas saat dikecilkan, serta tombol satukan kembali ke notch utama (`⇲`), sementara notch utama tetap tampil di atas layar.
- **Transformasi Otomatis menjadi Tombol Ikon saat Mode Pelebaran**:
  - Ketika **Pemutar Musik** atau **To-Do List** sedang dibuka ke jendela pelebaran/pelepasan, komponen tersebut di notch utama otomatis berubah menjadi **tombol ikon ringkas (`42×42`)** dengan indikator aktif hijau, dan kembali menjadi *pill* lengkap saat disatukan kembali (`⇲`).
- **Kartu Info Tentang DRANOTCH (`AboutWindow`) & Auto-Update 1-Klik**:
  - Mengklik tombol **Logo DRANOTCH** memunculkan kartu info ringkas dengan tombol cek pembaruan dan tautan ke [Halaman Rilis Resmi](https://dningratx.github.io/dranotch/).
  - Mengklik tombol versi (`v1.6.1`) langsung memeriksa pembaruan, mengunduh installer terbaru, dan menjalankan instalasi otomatis.
- **Perbaikan Hitung Mundur Subuh Hari Berikutnya & Penyederhanaan Pengaturan**:
  - Memperbaiki perhitungan hitung mundur menuju waktu **Subuh hari berikutnya** setelah waktu Isya berlalu.
  - Menyempurnakan kontras teks *dropdown* di jendela Pengaturan dan menempatkan bagian **Pembaruan Aplikasi** di urutan paling atas.

### 🎤 17. Penghilang Vokal *Real-Time* (Mode Karaoke DSP) & Lirik Tetap Tampil saat *Minimize* *(v1.6.2)*
- **Tombol Penghilang Vokal (`🎤 Karaoke`) *Real-Time***:
  - Tersedia di jendela **Pemutar Musik** (baik saat mode diperluas maupun saat di-*minimize*), di panel *Expanded* notch utama, serta di *bar* navigasi atas **YouTube Music Bawaan (`YouTubeMusicWindow`)**.
  - Menggunakan pemrosesan sinyal **Web Audio API DSP (*Mid-Side Phase Cancellation* `L - R`)** untuk meredam suara vokal utama di kanal tengah (`> 165 Hz`) secara *real-time* tanpa jeda saat memutar lagu di **YouTube Music Bawaan (`▶ YT Music`)** atau **Browser Mengambang**.
  - Dilengkapi **Pelestarian Bass & Kick Drum (`< 165 Hz`)** serta **Dynamics Compressor** agar musik pengiring tetap bulat, hangat, dan bertenaga untuk berkaraoke.
- **Lirik Tetap Tampil saat Mode *Minimize* Pelepasan Pemutar Musik**:
  - Ketika jendela pelepasan Pemutar Musik (`MusicWindow`) diperkecil (*minimize* `▴`), tampilan ringkas tetap menampilkan **kotak lirik 3 baris** secara langsung di bawah baris kontrol utama (tanpa *progress bar* dan *offset* agar tetap ringkas di layar).

### 🔼 18. Tombol Panah Bezel Atas/Bawah & Desain Kapsul Bezel Bersih Tanpa Bayangan Blur *(v1.6.4)*
- **Tombol Panah Atas/Bawah (`▲` / `▼`) Menempel di Tengah Garis Bezel Bawah**:
  - Menambahkan tombol panah ramping yang menempel persis di tengah garis tepi bawah bezel (`MainIslandBorder`) tanpa menambah tinggi ruang hitam di dalam notch.
  - **Minimize ke Bezel Atas (`▲`)**: Saat banyak jendela atau tab aplikasi lain sedang dibuka, klik tombol panah ke atas (`▲`) untuk melipat/mengecilkan notch *idle* menjadi strip bezel super tipis di tepi atas layar agar tidak menutupi *titlebar* atau *tab* jendela lain.
  - **Perlindungan Anti-Hover saat Diminimize**: Ketika dalam kondisi diminimize ke bezel atas, mengarahkan kursor ke tepi atas layar tidak akan memunculkan komponen-komponen notch secara tidak sengaja.
  - **Buka Kembali Notch (`▼`)**: Klik tombol panah ke bawah (`▼`) pada garis bezel (atau melalui menu klik kanan bezel) kapan saja untuk membuka kembali ukuran notch ke tampilan normal.
  - **Desain Kapsul Bezel Bersih Tanpa Bayangan Blur (`v1.6.4`)**: Menghapus bayangan hitam blur (`DropShadowEffect`) dan artefak garis lancip *concave ear* di sisi kiri-kanan, sehingga saat diminimize maupun dibuka tampil sebagai kapsul tunggal yang tajam, bersih, dan menyatu rapi di tepi atas layar.

### 📐 19. Resize Bebas Jendela To-Do List saat Mode Diperluas (*Expanded*) *(v1.6.5)*
- **Bebas Diperbesar / Diperkecil (*Free Resizing*)**: Saat jendela mengambang To-Do List berada dalam kondisi diperluas (*expanded*), jendela kini dapat diubah ukurannya secara bebas (lebar dan tinggi) sesuai preferensi dan kenyamanan membaca daftar tugas.
- **Indikator Pegangan Sudut Mewah (*Luxury Metallic Resize Grip*)**: Dilengkapi indikator grip garis diagonal modern di pojok kanan-bawah dengan kursor SizeNWSE dan efek hover perak mengilap.
- **Dukungan Resize dari Seluruh Tepi & Sudut**: Dilengkapi pegangan interaktif di tepi kanan, kiri, atas, bawah, serta sudut kiri-bawah dan sudut atas.
- **Tata Letak Adaptif & Ingat Ukuran Otomatis**: Daftar tugas dan kotak teks input tugas otomatis membentang secara responsif mengisi ruang baru, dan ukuran jendela terakhir otomatis tersimpan serta dipulihkan saat dibuka kembali.
- **Rapi saat Mode Compact**: Seluruh area resize otomatis dinonaktifkan saat jendela berada dalam mode *compact/ticker* mini agar tampilan kapsul tetap bersih dan ramping.

### ⏰ 20. Target Waktu Selesai (Deadline) To-Do List & Nada Pengingat Real-Time *(v1.6.6)*
- **Target Waktu Selesai pada Setiap Tugas**:
  - Pengguna dapat menentukan target tanggal dan jam selesai pada saat menambah tugas baru maupun saat mengedit tugas (*inline edit*).
  - Pilihan cepat preset (+1 Jam, +3 Jam, Hari ini 17:00, Malam 21:00, Besok 09:00, Besok 17:00) serta pemilih kustom tanggal (`DatePicker`) dan jam/menit (0-23 : 0-59).
  - Dialog pengaturan target waktu mandiri, rapi, dan terisolasi antara jendela notch utama dan jendela To-Do List mengambang (*detached*).
- **Indikator Status & Ikon Jam Presisi**:
  - Dilengkapi ikon jam vektor tajam dan badge waktu dinamis yang berubah warna (kuning saat aktif mendekati batas waktu, merah saat terlewat/*overdue*, biru muda saat santai).
- **Pengingat Nada & Notifikasi Real-Time**:
  - Saat waktu deadline tercapai, aplikasi otomatis membunyikan alert bell melodik dua nada (`Ding-Dong`) dan menampilkan banner notifikasi pengingat tugas.

### 🌐 21. Isolasi Audio Karaoke Browser & Teks Pembaruan Interaktif *(v1.6.6)*
- **Isolasi DSP Karaoke untuk Browser Mengambang**: Suara video atau web pada Browser Mengambang (*Floating Browser*) tidak lagi ikut teredam atau masuk mode karaoke saat Karaoke Vocal Remover aktif di pemutar musik.
- **Teks Pembaruan Versi Interaktif ("Click to Update {versi}")**: Saat ada rilis baru terdeteksi, label versi di pengaturan otomatis berubah menjadi link klik interaktif untuk mempermudah pembaruan langsung.

### ⚡ 22. Sinkronisasi Instan Google Drive To-Do List & Pembersihan Pengaturan *(v1.6.7)*
- **Sinkronisasi Instan Tanpa Tunda (0ms Delay)**: Setiap tindakan pada To-Do list—menghapus tugas, menambah tugas baru, mencentang/membatalkan centang selesai, mengedit tugas (*inline edit*), mengubah target tenggat waktu, membersihkan tugas selesai, maupun menyusun ulang urutan tugas (*drag & drop*)—langsung disinkronkan ke Google Drive secara instan tanpa jeda *debounce*.
- **Pelacakan ID yang Dihapus (*Tombstone Tracking*)**: Menambahkan sistem pelacakan ID tugas yang dihapus (`deleted_todos.json`), menjamin tugas yang telah dihapus di lokal tidak akan pernah muncul kembali (*resurrect*) saat sinkronisasi dua arah atau auto-sync cloud berjalan.
- **Pembersihan Antarmuka Pengaturan Waktu Sholat**: Menghapus opsi *"Pisahkan Waktu Sholat ke jendela terpisah (tidak menempel dengan musik)"* dari jendela Pengaturan untuk pengalaman pengguna yang lebih bersih, fokus, dan ringkas.
- **Pembaruan Status UI Google Drive Responsif**: Indikator status sinkronisasi kini otomatis di-*dispatch* langsung ke UI thread, sehingga indikator tombol Drive (`Menyinkronkan...` -> `Drive` / `Tersinkron`) langsung terupdate tanpa *lag*.

### 🌳 23. Struktur Cabang To-Do Kategori & Hitung Mundur Sisa Waktu *(v1.6.8)*
- **Hierarki Cabang Visual Kategori (*Tree/Branch Structure*)**: Item to-do otomatis dikelompokkan rapi di bawah kategori tugasnya dengan header folder (`📁`), nama kategori tebal, badge jumlah item, dan garis penghubung cabang visual (`├──•`) yang elegan.
- **Tampilan Bersih Bebas Tag Berulang**: Menghilangkan badge kategori berulang di dalam setiap baris tugas karena tugas sudah tertata di bawah cabang kategorinya masing-masing.
- **Drag-and-Drop Lintas Kategori**: Memindahkan tugas dari satu kategori ke kategori lain kini semudah menyeret item ke kelompok kategori tujuan, kategori tugas akan otomatis diperbarui.
- **Hitung Mundur Sisa Waktu & Hari Real-Time (*Remaining Time & Days*)**: Setiap tugas dengan target waktu kini menampilkan sisa hari, jam, dan menit secara langsung (contoh: `Besok 17:00 • Sisa 1 hr 4 jam`, `Hari ini 17:00 • Sisa 3 jam 20 mnt`, `Sisa 45 mnt`, atau `⚠️ Terlewat 2 jam`), diperbarui otomatis setiap 2 detik.

### 🎵 24. Ukuran Bebas Browser YouTube Music, Preset Cepat & Mode Mengambang Pin *(v1.6.9)*
- **Bebas Diperbesar / Diperkecil (*Free Resizing*)**: Jendela pemutar bawaan YouTube Music (`YouTubeMusicWindow`) kini dapat diubah ukurannya secara bebas dari seluruh tepi dan sudut jendela (*WindowChrome 8px*) dengan batas minimal hingga `360 × 240` untuk mode mini player yang sangat ringkas.
- **Pilihan Ukuran Cepat Preset (`📐`)**: Tombol preset ukuran instan di title bar menyediakan pilihan rasio sekali klik: Mini PiP (400×260), Ringkas (640×420), Sedang (840×560), Standar (1080×720), Layar Lebar HD (1280×800), serta kontrol perbesar/perkecil (+15% / -15%) dan skala zoom konten (75% - 125%).
- **Mode Mengambang Pin (`📌 Always On Top`)**: Sematkan jendela YouTube Music agar selalu berada di atas aplikasi lain saat memutar musik sambil bekerja, mengetik, atau coding.
- **Penyimpanan Dimensi Otomatis**: Ukuran dan posisi jendela terakhir otomatis tersimpan di pengaturan dan tidak akan ter-reset saat jendela dibuka ulang melalui pemutar musik atau menu tray.

### 🕌 25. Pilihan Suara Azan Terbaik Dunia, Durasi Kustom & Timeline Audio Interaktif *(Baru di v1.8.0)*
- **9 Pilihan Audio Azan Terbaik Dunia**: Pilihan lantunan azan dari Masjidil Haram Makkah, Masjid Nabawi Madinah, Masjid Al-Aqsa Yerusalem, Syekh Mishary Rashid Alafasy, Syekh Abdul Basit Abdus Samad, Syekh Mansoor Az-Zahrani, Syekh Mohammad Refaat (Mesir klasik), Hafiz Mustafa Özcan (Turki Utsmani), dan Azan Subuh khusus (*Ash-shalatu khairum minan naum*), serta Chime dan file audio kustom (.mp3/.wav).
- **Pemotongan Durasi Azan Kustom (Menit & Detik)**: Fitur membuat azan pendek sesuai rentang menit dan detik yang diinginkan dengan tombol preset instan (30 Detik, 1 Menit, 2 Menit, dan Penuh) serta ringkasan durasi real-time.
- **Panel Kontrol Pemutar & Pelacak Detik Real-Time (*Audio Timeline Scrubber*)**: Pantau posisi detik audio secara langsung (`Posisi: 00:14 / Total: 03:45`), slider interaktif untuk melompat ke detik mana saja, tombol Play/Pause/Stop, serta tombol cerdas `📍 Set Jadi Detik Mulai` dan `📍 Set Jadi Detik Selesai` untuk menandai detik potong otomatis saat mendengarkan.
- **Mini Player di Jendela Waktu Sholat**: Saat tombol `Tes` diklik pada jendela waktu sholat, bar progress dan counter waktu aktif (`⏱ 00:14 / 01:00`) langsung muncul dengan tombol pause dan stop.

### 📱 26. Ukuran Default Smartphone YouTube Music Browser & Pembersihan Mode Karaoke *(Baru di v1.8.0)*
- **Default Seukuran Handphone (`380 × 740`)**: Saat jendela browser YouTube Music dibuka, ukuran default kini menyerupai layar smartphone vertikal yang proporsional, hemat ruang, dan nyaman diletakkan di samping monitor.
- **Pembersihan Mode Karaoke**: Menghilangkan tombol karaoke dari jendela browser YouTube Music untuk pengalaman browsing yang lebih bersih dan terfokus.

### 📂 27. Tombol Expand & Collapse Kategori To-Do List *(v1.8.0)*
- **Buka & Tutup Per Folder Kategori**: Setiap folder kategori dilengkapi ikon chevron interaktif (`▾` / `▸`) untuk membuka atau menyembunyikan daftar tugas di bawahnya.
- **Tombol Global Buka Semua & Tutup Semua**: Tombol pill `⊞ Buka Semua` dan `⊟ Tutup Semua` untuk merapikan seluruh kategori dalam satu klik.

### 💚 28. Pemutar Spotify Web Bawaan, Saling Jeda Pintar & Ikon Presisi *(v1.8.2)*
- **Pemutar Spotify Web Bawaan (`SpotifyWindow`)**: Pemutar bawaan terisolasi berbasis WebView2 dengan penyimpanan sesi login akun Spotify permanen, ukuran default handphone (`380 × 740`), preset ukuran instan, dan pemutaran latar belakang.
- **Saling Jeda Cerdas (*Mutual Auto-Pause on Play*)**: Saat menekan Play di YouTube Music, pemutar Spotify otomatis dijeda, dan sebaliknya saat menekan Play di Spotify, YouTube Music otomatis dijeda.
- **Ikon YouTube Music & Spotify Presisi (`26 × 26 px`)**: Desain tombol ikon simetris tanpa label teks di kapsul notch.

### 📺 29. Komponen ke-6: Live TV Indonesia & Dunia *(v1.8.3)*
- **6 Komponen Modular Terintegrasi**: Menambahkan komponen **Live TV** di Dynamic Notch dan System Tray, mendampingi Pemutar Musik, Radio Online, Waktu Sholat, To-Do List, dan Browser Mengambang.
- **Streaming TV Langsung Indonesia & Internasional (`LiveTvWindow`)**:
  - Katalog saluran TV nasional Indonesia (RCTI, SCTV, Indosiar, Trans TV, Trans 7, Kompas TV, Metro TV, tvOne, CNN Indonesia, CNBC Indonesia, TVRI Nasional, RTV, BTV, Garuda TV, Nusantara TV, Moji, NET., Rodja TV, TVMu, dll.) serta saluran berita, olahraga, film, anak, dan dokumenter dunia (Al Jazeera, CNA, NHK World, DW, France 24, Euronews, NASA TV, Red Bull TV, dll.).
  - **Pemutar Hybrid HLS (`.m3u8`) & YouTube Live**: Mendukung pemutaran langsung aliran IPTV HLS (`hls.js`) maupun siaran langsung YouTube dengan pergantian otomatis jalur stream cadangan (*Multi-Source Failover* `🔄`) apabila salah satu server sedang gangguan.
  - **Filter Negara & Kategori + Pencarian Online Dunia**: Pilih puluhan negara (`🇮🇩 Indonesia`, `🇲🇾 Malaysia`, `🇸🇬 Singapura`, `🇸🇦 Arab Saudi`, `🇯🇵 Jepang`, `🇰🇷 Korea Selatan`, `🇬🇧 Inggris`, `🇺🇸 Amerika Serikat`, dll.), filter kategori (*Berita*, *Hiburan*, *Olahraga*, *Religi*, *Musik*, *Anak*, *Dunia*), dan cari ribuan saluran TV dari seluruh dunia secara online.
  - **Mode Bioskop (`⛶`), Mini Picture-in-Picture (`📺 PiP`), & Saluran Custom (`+ URL`)**: Sembunyikan daftar saluran untuk menonton layar penuh di jendela mengambang, perkecil ke jendela Mini TV PiP yang selalu tampil di atas aplikasi lain, tandai saluran favorit (`★`), atau tambahkan URL `.m3u8` / YouTube Live milik Anda sendiri.
  - **Koordinasi Audio Pintar**: Memutar saluran Live TV secara otomatis menghentikan pemutaran Radio Online agar suara tidak bertumpuk.

### 🌤️ 30. Komponen ke-7: Cuaca & Gempa Bumi BMKG, Lokasi Presisi Kelurahan/Desa & Mode Idle Minimalis *(v1.8.4)*
- **Integrasi Resmi Data Terbuka BMKG (`BmkgWindow`)**:
  - Menampilkan **Prakiraan Cuaca Per 3 Jam (3 Hari)** langsung dari API publik resmi BMKG (`api.bmkg.go.id`), mencakup suhu udara (`°C`), kondisi langit, kelembapan (`%`), kecepatan & arah mata angin (`km/j`), tutupan awan (`%`), serta curah hujan (`mm`).
  - Memantau **Informasi Gempa Bumi Real-Time (`data.bmkg.go.id`)** dalam 3 tab: **Gempa Terbaru (*AutoGempa*)** lengkap dengan pratinjau visual **Peta Guncangan (*Shakemap BMKG*)**, **15 Gempa Terkini (`M ≥ 5.0`)**, dan **15 Gempa Dirasakan (skala MMI)** beserta tautan langsung titik episentrum di Google Maps.
- **Lokasi Presisi hingga Tingkat Kelurahan / Desa (`adm4`)**:
  - **Pencarian Lokasi Langsung (*Live Search*)**: Ketik nama Kelurahan/Desa, Kecamatan, atau Kota di seluruh Indonesia untuk menemukan kode wilayah `adm4` secara akurat beserta titik koordinatnya.
  - **Pemilih Wilayah Bertingkat 4 Level (`Pilih Wilayah`)**: Pilih berjenjang mulai dari **Provinsi (38 Provinsi)** ▸ **Kabupaten/Kota** ▸ **Kecamatan** ▸ **Kelurahan/Desa**.
  - **Deteksi Lokasi Otomatis Presisi & Sinkronisasi Waktu Sholat**: Tombol **Deteksi** otomatis memetakan koordinat ke tingkat Kelurahan/Desa terdekat dan menyinkronkan koordinat jadwal Waktu Sholat secara presisi.
- **Ikon Vektor Cuaca Berwarna Terang & Banner Peringatan Dini**:
  - Seluruh ikon cuaca (Cerah, Cerah Berawan, Berawan, Udara Kabur/Kabut, Hujan, Hujan Petir) dan indikator dirancang menggunakan **ikon vektor SVG berwarna kontras tinggi** di atas tema gelap OLED.
  - Dilengkapi **Banner Peringatan Dini Dynamic Island** otomatis saat terdeteksi gempa bumi baru atau peringatan cuaca ekstrem (hujan lebat/petir).
- **Mode Idle Minimalis (Hanya Logo DRANOTCH)**:
  - Saat kondisi *idle* (kursor tidak diarahkan dan notch tidak di-pin), Dynamic Notch hanya menampilkan **Logo DRANOTCH** yang bersih dan minimalis di tengah atas layar. Saat di-*hover*, seluruh komponen dan informasi ringkas muncul secara simetris.

### 💧 31. Komponen ke-8: Pengingat Pintar (Minum Air, Minum Obat, Istirahat Mata & Aktivitas) *(Baru di v1.8.5)*
- **Jendela Mandiri Pengingat Pintar (`ReminderWindow`)**:
  - Mendukung 2 tipe jadwal pengingat: **Interval Berulang** (misal setiap 15m, 20m, 30m, 45m, 1 Jam, 2 Jam untuk minum air putih, istirahat mata 20-20-20, peregangan) dan **Jam Harian Tertentu** (misal pukul `08:00, 13:00, 20:00` untuk minum obat atau vitamin).
  - Dilengkapi **Bar Preset Pintar 1-Klik** (`Minum Air 1 Jam`, `Minum Obat 3x Sehari`, `Istirahat Mata 20m`, `Peregangan 45m`), form kustomisasi lengkap, pencatatan jumlah selesai harian (`✓ Nx hari ini`), serta tombol cepat **Selesai (`✓`)**, **Tunda 10 Menit (`💤 +10m`)**, dan **Reset Timer (`↻`)**.
- **Ikon Vektor SVG Berwarna Terang & Kontras Tinggi**:
  - Setiap jenis pengingat ditampilkan dengan ikon vektor SVG tajam di dalam *badge* berwarna khusus: Tetesan Air Biru Cerah (`#38BDF8`), Kapsul Obat Rose (`#FB7185`), Mata Ungu Terang (`#C084FC`), Peregangan Hijau Zamrud (`#34D399`), Makan/Minum Amber (`#FBBF24`), Olahraga Teal (`#2DD4BF`), dan Alarm Biru (`#60A5FA`).
- **Tata Letak Simetris Bar Bawah Saat Hover**:
  - Saat *hover*, bar informasi bawah tersusun simetris: **Cuaca & Gempa BMKG di sebelah kiri**, **Waktu Sholat tepat di tengah**, dan **Pengingat Pintar di sebelah kanan** yang menampilkan seluruh ikon pengingat aktif beserta sisa waktunya secara ringkas.
- **Banner Notifikasi Melayang Dynamic Island & Suara Pengingat**:
  - Menampilkan kartu notifikasi melayang otomatis beserta suara pengingat saat jadwal tiba, dilengkapi aksi cepat **`✓ Sudah`** dan **`💤 +10m`** langsung dari notch.

### 🌟 32. Mode Cerdas Auto-Window, Animasi Transisi Halus & Koleksi Suara Alarm Baru *(Baru di v1.8.6)*
- **Mode Pintar Deteksi Jendela Aktif (Auto-Window)**:
  - **0 Jendela (Desktop Bersih)**: Notch secara otomatis membuka **Mode Expand** menampilkan seluruh tombol media, radio, TV, browser, BMKG, pengingat, to-do, dan info bar sholat/cuaca mekar penuh.
  - **1 Jendela Terbuka**: Notch otomatis beralih ke **Mode Standby** ringkas (hanya kapsul elegan logo DRANOTCH "D" di tengah atas).
  - **≥ 2 Jendela Terbuka**: Notch otomatis beralih ke **Mode Minimize** (panah kecil `v` di atas bezel layar) agar tidak menutupi area multitasking pengguna.
- **Animasi Transisi Halus (*Smooth Transitions*)**:
  - Seluruh transisi perbesaran notch (*Expand*), pengecilan (*Standby*), dan penyembunyian ke bezel (*Minimize*) menggunakan akselerasi kurva halus (*Cubic Ease-In-Out*).
  - Panel samping dan baris bawah otomatis di-*collapse* secara instan saat animasi selesai, menghilangkan glitch ruang kosong hitam.
- **Interaksi Logo DRANOTCH Cerdas**:
  - Di Mode Standby, klik logo DRANOTCH untuk langsung melebarkan notch ke Mode Expand.
  - Di Mode Expand, klik logo DRANOTCH untuk membuka jendela informasi About DRANOTCH.
- **8 Pilihan Suara Alarm Pengingat Bawaan (Bebas Royalti)**:
  - Tersedia 8 varian audio alarm berkualitas tinggi (.wav): *Digital Pulse*, *Crystal Drop*, *Classic Bell*, *Marimba*, *Morning Breeze*, *Radar*, *Reflection*, dan *Zen Bowl*.
  - Pemilihan jenis nada alarm kustom per pengingat dan tombol pratinjau tes suara di Pengaturan.

### 📑 33. Komponen ke-9: Catatan Tempel (Sticky Notes) & Sinkronisasi Google Drive *(Baru di v1.9.0)*
- **Jendela Catatan Tempel Melayang di Desktop (`StickyNoteWindow`)**:
  - Catatan tempel mandiri yang bisa digeser dan ditempatkan bebas di mana saja pada layar desktop.
  - **Ukuran Proporsional Standar Windows Sticky Notes**: Default ukuran baru `320×320` piksel dengan batas minimal fleksibel (`MinWidth="200"`, `MinHeight="160"`).
  - **7 Pilihan Warna Pastel & Gelap Elegan**: Kuning (*Yellow*), Hijau (*Green*), Pink (*Pink*), Biru (*Blue*), Ungu (*Purple*), Oranye (*Orange*), dan Gelap (*Dark OLED*).
  - **Fitur Catatan Lengkap**:
    - **Ubah Ukuran Bebas 8-Arah (*8-Direction Fluid Resizing*)**: Tarik dan sesuaikan ukuran catatan dari 4 sisi (Kiri, Kanan, Atas, Bawah) maupun 4 sudut (Kiri-Atas, Kanan-Atas, Kiri-Bawah, Kanan-Bawah) dengan perubahan kursor dinamis.
    - **Pergeseran Catatan Halus (*Fluid Drag & Move*)**: Seluruh bidang header dilengkapi ikon pegangan `⠿` dan kursor `SizeAll` serta bar footer dapat langsung diseret untuk memindahkan catatan.
    - **Ganti Judul Instan**: Klik dua kali (*double-click*) pada judul catatan di header untuk mengubah nama catatan seketika.
    - **Area Penulisan Bersih**: Bebas dari teks watermark placeholder sehingga siap digunakan mengetik langsung layaknya Sticky Notes fisik.
    - **Selalu di Atas (`📌`)**: Fitur *Always on Top* independen per catatan agar tetap tampak di atas jendela aplikasi lain.
    - **Toolbar Header Minimalis**: Tombol praktis untuk Buat Baru (`➕`), Ubah Warna (`●`), Pin (`📌`), Hapus (`🗑`), dan Tutup/Sembunyikan (`✕`).
    - **Status Diedit & Indikator Cloud**: Menampilkan waktu terakhir diedit serta jumlah karakter secara real-time.
- **Hub Koleksi Catatan Tempel (`StickyNotesHubWindow`)**:
  - Pusat manajemen seluruh catatan dengan **Tata Letak Per Baris (*List Row View*)** memanjang yang bersih dan rapi.
  - **Indikator Warna & Status Badge**: Garis aksen warna vertikal, judul tebal, cuplikan pratinjau teks (snippet), waktu pembaruan, serta lencana status (`Di Layar` atau `Tersimpan`).
  - **Pencarian Cepat (*Instant Search*)**: Temukan catatan seketika berdasarkan kata kunci pada judul maupun isi catatan.
  - **Aksi Cepat & Massal**: Tombol Buka/Fokus dan Hapus per baris, serta tombol buka serentak (`Buka Semua`) atau sembunyikan semua dari layar monitor (`Tutup Semua`).
  - **Jendela Hub Bebas Ubah Ukuran (*Resizable Hub Window*)**: Jendela Hub dapat diperbesar dan dikecilkan dari seluruh sisi & sudut 8-arah dengan posisi serta ukuran yang tersimpan secara persisten.
- **Sinkronisasi Otomatis Google Drive 2-Arah**:
  - Terintegrasi penuh ke Google Drive di folder aman `DRANOTCH/stickynotes.json`.
  - **Auto-Sync Cerdas**: Setiap perubahan teks, warna, judul, atau penambahan catatan baru otomatis diunggah ke Google Drive di latar belakang.
  - **Merge & Tombstone Tracking**: Menggabungkan data catatan dari perangkat lain secara pintar dengan resolusi konflik berbasis waktu terbaru (*last-write-wins*), serta pelacakan ID yang dihapus (`deleted_stickynotes.json`) agar catatan yang telah dibuang tidak muncul kembali.
- **Integrasi Notch & System Tray**:
  - Ikon Catatan Tempel tersemat di Notch bar sisi kanan, lengkap dengan titik indikator emas saat ada catatan aktif di layar, menu System Tray, serta tombol konfigurasi di Pengaturan.

---

## 📸 Tampilan Antarmuka

| Mode | Deskripsi |
| :--- | :--- |
| **Compact Notch** | Menampilkan artwork album, status lagu/lirik berjalan, ticker tugas aktif, pill cuaca BMKG, waktu sholat di tengah, dan ikon pengingat aktif di kanan. |
| **Expanded Panel** | Panel gabungan kontrol musik lengkap, progress seekbar, lirik, dan daftar tugas To-Do. |
| **Detached To-Do** | Jendela To-Do mandiri dengan tema gelap OLED monokrom, kontrol minimize independen, drag & drop bebas, dan auto-save posisi. |
| **Lyrics Overlay** | Tampilan lirik berukuran besar dengan scrolling halus otomatis mengikuti vokal lagu. |
| **Prayer Card** | Kartu jadwal sholat harian lengkap, kalender Hijriah, hitung mundur real-time, dan status adzan. |

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

1. Unduh installer setup terbaru: [**`DRANOTCH-Setup-1.9.0.exe`**](https://github.com/dningratx/dranotch/releases/latest).
2. Jalankan berkas installer dan ikuti petunjuk pemasangan.
3. DRANOTCH akan muncul di bagian atas layar desktop Anda.

> **🛡️ Notifikasi Keamanan Windows SmartScreen ("Windows protected your PC"):**  
> Karena DRANOTCH adalah proyek independen yang ditandatangani secara digital dengan *Self-Signed Certificate*, Windows SmartScreen mungkin menampilkan layar biru peringatan.  
> Anda dapat melewatinya dengan salah satu cara berikut:
> - **Cara Cepat**: Klik **"More info"** (*Info selengkapnya*) lalu klik **"Run anyway"** (*Tetap jalankan*).
> - **Atau Buka Blokir**: Klik kanan file installer -> **Properties** -> centang **"Unblock"** -> **OK**.
> - **Permanen Tepercaya**: Jalankan berkas **`Install-Certificate.bat`** (yang disertakan di folder rilis/dist) sekali saja untuk mendaftarkan sertifikat DRANOTCH ke *Trusted Root Certification Authorities* di komputer Anda.

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
- **Web Runtime**: [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (untuk pemutar YouTube Music & Spotify)
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

