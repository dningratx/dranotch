using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows.Media;
using NotchMusic.Models;

namespace NotchMusic.Helpers;

public static class PrayerAudioHelper
{
    private static byte[]? _cachedChimeWav;
    private static byte[]? _cachedDeadlineWav;
    private static MediaPlayer? _mediaPlayer;
    private static System.Windows.Threading.DispatcherTimer? _positionTimer;
    private static double _targetStartSeconds;
    private static double _targetEndSeconds;
    private static bool _isPlaying;
    private static bool _isPaused;
    private static Action? _currentOnEnded;

    public static bool IsPlaying => _isPlaying;
    public static bool IsPaused => _isPaused;
    public static double CurrentPositionSeconds { get; private set; }
    public static double TotalDurationSeconds { get; private set; }
    public static double TargetStartSeconds => _targetStartSeconds;
    public static double TargetEndSeconds => _targetEndSeconds;

    /// <summary>
    /// Event yang dipicu setiap kali posisi playback berubah (posisiSaatIni, totalDurasi dalam detik).
    /// </summary>
    public static event Action<double, double>? PlaybackProgressChanged;

    /// <summary>
    /// Event yang dipicu saat status playback berubah (Play, Pause, Stop).
    /// </summary>
    public static event Action? PlaybackStateChanged;

    public static readonly IReadOnlyList<AdhanSoundOption> AvailableSounds = new List<AdhanSoundOption>
    {
        new()
        {
            Id = "makkah",
            Name = "Adzan Mekkah (Masjidil Haram)",
            Description = "Muazin Masjidil Haram, sangat syahdu, merdu, dan penuh keagungan.",
            Icon = "🕋",
            FileName = "adhan_makkah.mp3"
        },
        new()
        {
            Id = "madinah",
            Name = "Adzan Madinah (Masjid Nabawi)",
            Description = "Lantunan khas Masjid Nabawi, sangat damai, sejuk, dan khusyuk.",
            Icon = "🕌",
            FileName = "adhan_madinah.mp3"
        },
        new()
        {
            Id = "alaqsa",
            Name = "Adzan Al-Aqsa (Yerusalem)",
            Description = "Lantunan syahdu dan menggetarkan hati dari Masjid Al-Aqsa, Palestina.",
            Icon = "🕌",
            FileName = "adhan_alaqsa.mp3"
        },
        new()
        {
            Id = "alafasy",
            Name = "Adzan Mishary Rashid Alafasy",
            Description = "Salah satu lantunan adzan terindah dan paling populer di seluruh dunia Islam.",
            Icon = "✨",
            FileName = "adhan_alafasy.mp3"
        },
        new()
        {
            Id = "abdulbasit",
            Name = "Adzan Syekh Abdul Basit Abdus Samad",
            Description = "Qari legendaris dunia dengan teknik vokal dan nafas panjang yang sangat khas.",
            Icon = "🎙️",
            FileName = "adhan_abdulbasit.mp3"
        },
        new()
        {
            Id = "mansoor",
            Name = "Adzan Syekh Mansoor Az-Zahrani",
            Description = "Lantunan dengan nada tinggi yang jernih, indah, dan menggetarkan jiwa.",
            Icon = "🌟",
            FileName = "adhan_mansoor.mp3"
        },
        new()
        {
            Id = "refaat",
            Name = "Adzan Mesir (Syekh Mohammad Refaat)",
            Description = "Adzan klasik Timur Tengah yang sarat kelembutan dan nuansa spiritual mendalam.",
            Icon = "🏛️",
            FileName = "adhan_refaat.mp3"
        },
        new()
        {
            Id = "turkey",
            Name = "Adzan Turki (Hafiz Mustafa Özcan)",
            Description = "Maqam Ussak khas Istanbul yang tenang, elegan, dan menyejukkan batin.",
            Icon = "🇹🇷",
            FileName = "adhan_turkey.mp3"
        },
        new()
        {
            Id = "fajr",
            Name = "Adzan Subuh Khusus (Malek Chebae)",
            Description = "Adzan fajar dengan lafadz istimewa 'Ash-Shalatu Khairum Minan-Naum'.",
            Icon = "🌅",
            FileName = "adhan_fajr.mp3"
        },
        new()
        {
            Id = "chime",
            Name = "Chime Harmoni (Bawaan Ringkas)",
            Description = "Nada lonceng harmoni ringkas, sangat cocok untuk lingkungan kantor / meeting.",
            Icon = "🔔",
            FileName = ""
        },
        new()
        {
            Id = "custom",
            Name = "File Audio Kustom (.mp3 / .wav)",
            Description = "Gunakan file suara adzan atau audio pilihan Anda sendiri dari komputer.",
            Icon = "📁",
            FileName = ""
        }
    };

    /// <summary>
    /// Memutar suara pengingat sholat sesuai tipe event dan konfigurasi AppSettings pengguna.
    /// Mendukung pemotongan durasi adzan jika AdhanCustomDurationEnabled aktif.
    /// </summary>
    public static void PlayPrayerNotification(AppSettings settings, PrayerNotificationEvent evt)
    {
        if (!settings.PrayerSoundAlert) return;

        // Saat Waktu Sholat Tiba (Exact/Adzan): Putar Suara Adzan Terpilih!
        if (evt.Type == PrayerReminderType.AtPrayerTime)
        {
            string soundId = settings.SelectedAdhanSound;

            // Jika sholat Subuh dan pengguna memilih fajr atau opsi default
            if (string.Equals(evt.PrayerName, "Subuh", StringComparison.OrdinalIgnoreCase) && soundId == "fajr")
            {
                soundId = "fajr";
            }

            double startSec = settings.GetAdhanStartTotalSeconds();
            double endSec = settings.GetAdhanEndTotalSeconds();

            PlayAdhan(soundId, settings.CustomAdhanSoundPath, settings.AdhanVolume, startSec, endSec);
        }
        else
        {
            // Untuk pengingat Sebelum & Sesudah: bunyikan chime lembut agar tidak mengagetkan
            PlayChime();
        }
    }

    /// <summary>
    /// Memutar suara adzan berdasarkan ID pilihan atau file kustom dengan rentang detik tertentu.
    /// </summary>
    public static void PlayAdhan(string? soundId, string? customPath = null, double volume = 0.85, double startSeconds = 0, double endSeconds = 0, Action? onEnded = null)
    {
        soundId ??= "makkah";

        if (string.Equals(soundId, "chime", StringComparison.OrdinalIgnoreCase))
        {
            PlayChime();
            onEnded?.Invoke();
            return;
        }

        string? resolvedPath = ResolveAudioFilePath(soundId, customPath);
        if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
        {
            // Fallback ke chime jika file tidak ditemukan
            PlayChime();
            onEnded?.Invoke();
            return;
        }

        PlayMediaFile(resolvedPath, volume, startSeconds, endSeconds, onEnded);
    }

    /// <summary>
    /// Memutar preview suara adzan saat pengguna memilih opsi di Pengaturan atau Waktu Sholat.
    /// </summary>
    public static void PlayPreview(string? soundId, string? customPath = null, double volume = 0.85, double startSeconds = 0, double endSeconds = 0, Action? onEnded = null)
    {
        StopAudio();
        PlayAdhan(soundId, customPath, volume, startSeconds, endSeconds, onEnded);
    }

    /// <summary>
    /// Menjeda (pause) pemutaran audio adzan saat sedang ditest.
    /// </summary>
    public static void PauseAudio()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_mediaPlayer != null && _isPlaying && !_isPaused)
            {
                try
                {
                    _mediaPlayer.Pause();
                    _isPaused = true;
                    _positionTimer?.Stop();
                    PlaybackStateChanged?.Invoke();
                }
                catch { }
            }
        });
    }

    /// <summary>
    /// Melanjutkan (resume) pemutaran audio adzan setelah di-pause.
    /// </summary>
    public static void ResumeAudio()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_mediaPlayer != null && _isPlaying && _isPaused)
            {
                try
                {
                    _mediaPlayer.Play();
                    _isPaused = false;
                    _positionTimer?.Start();
                    PlaybackStateChanged?.Invoke();
                }
                catch { }
            }
        });
    }

    /// <summary>
    /// Menggeser (seek) posisi audio adzan ke detik tertentu.
    /// </summary>
    public static void SeekTo(double totalSeconds)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_mediaPlayer != null)
            {
                try
                {
                    double maxDuration = TotalDurationSeconds > 0 ? TotalDurationSeconds : 3600;
                    double clamped = Math.Clamp(totalSeconds, 0, maxDuration);
                    _mediaPlayer.Position = TimeSpan.FromSeconds(clamped);
                    CurrentPositionSeconds = clamped;
                    PlaybackProgressChanged?.Invoke(CurrentPositionSeconds, TotalDurationSeconds);
                }
                catch { }
            }
        });
    }

    /// <summary>
    /// Menghentikan pemutaran audio adzan yang sedang aktif.
    /// </summary>
    public static void StopAudio()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                if (_positionTimer != null)
                {
                    _positionTimer.Stop();
                }

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Close();
                }
            }
            catch { }
            _targetEndSeconds = 0;
            _targetStartSeconds = 0;
            _isPlaying = false;
            _isPaused = false;
            CurrentPositionSeconds = 0;
            PlaybackStateChanged?.Invoke();
            PlaybackProgressChanged?.Invoke(0, TotalDurationSeconds);
            _currentOnEnded?.Invoke();
            _currentOnEnded = null;
        });
    }

    private static void PlayMediaFile(string filePath, double volume, double startSeconds, double endSeconds, Action? onEnded)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                StopAudio();

                _currentOnEnded = onEnded;
                _targetStartSeconds = startSeconds;
                _targetEndSeconds = endSeconds > startSeconds ? endSeconds : 0;
                CurrentPositionSeconds = startSeconds;
                TotalDurationSeconds = 0;

                _mediaPlayer ??= new MediaPlayer();

                _mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
                _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
                _mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
                _mediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;

                _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

                _mediaPlayer.Volume = Math.Clamp(volume, 0.0, 1.0);
                _mediaPlayer.Open(new Uri(filePath, UriKind.Absolute));
                _mediaPlayer.Play();
                _isPlaying = true;
                _isPaused = false;

                PlaybackStateChanged?.Invoke();

                _positionTimer ??= new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _positionTimer.Tick -= PositionTimer_Tick;
                _positionTimer.Tick += PositionTimer_Tick;
                _positionTimer.Start();
            }
            catch
            {
                _isPlaying = false;
                _isPaused = false;
                PlaybackStateChanged?.Invoke();
                PlayChime();
                onEnded?.Invoke();
            }
        });
    }

    private static void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_isPlaying && !_isPaused && _mediaPlayer != null)
        {
            try
            {
                if (_mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    TotalDurationSeconds = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                }

                CurrentPositionSeconds = _mediaPlayer.Position.TotalSeconds;
                PlaybackProgressChanged?.Invoke(CurrentPositionSeconds, TotalDurationSeconds);

                if (_targetEndSeconds > 0 && CurrentPositionSeconds >= _targetEndSeconds)
                {
                    StopAudio();
                }
            }
            catch { }
        }
    }

    private static void MediaPlayer_MediaOpened(object? sender, EventArgs e)
    {
        if (_mediaPlayer != null)
        {
            try
            {
                if (_mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    TotalDurationSeconds = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                }

                if (_targetStartSeconds > 0)
                {
                    _mediaPlayer.Position = TimeSpan.FromSeconds(_targetStartSeconds);
                    CurrentPositionSeconds = _targetStartSeconds;
                }
                else
                {
                    CurrentPositionSeconds = 0;
                }

                PlaybackProgressChanged?.Invoke(CurrentPositionSeconds, TotalDurationSeconds);
                PlaybackStateChanged?.Invoke();
            }
            catch { }
        }
    }

    private static void MediaPlayer_MediaEnded(object? sender, EventArgs e)
    {
        _isPlaying = false;
        _isPaused = false;
        _positionTimer?.Stop();
        CurrentPositionSeconds = TotalDurationSeconds;
        PlaybackStateChanged?.Invoke();
        PlaybackProgressChanged?.Invoke(CurrentPositionSeconds, TotalDurationSeconds);
        _currentOnEnded?.Invoke();
        _currentOnEnded = null;
    }

    private static void MediaPlayer_MediaFailed(object? sender, ExceptionEventArgs e)
    {
        _isPlaying = false;
        _isPaused = false;
        _positionTimer?.Stop();
        PlaybackStateChanged?.Invoke();
        _currentOnEnded?.Invoke();
        _currentOnEnded = null;
    }

    private static string? ResolveAudioFilePath(string soundId, string? customPath)
    {
        if (string.Equals(soundId, "custom", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            {
                return customPath;
            }
            // Fallback ke makkah jika file custom belum diset
            soundId = "makkah";
        }

        string fileName = soundId switch
        {
            "makkah" => "adhan_makkah.mp3",
            "madinah" => "adhan_madinah.mp3",
            "alaqsa" => "adhan_alaqsa.mp3",
            "alafasy" => "adhan_alafasy.mp3",
            "abdulbasit" => "adhan_abdulbasit.mp3",
            "mansoor" => "adhan_mansoor.mp3",
            "refaat" => "adhan_refaat.mp3",
            "turkey" => "adhan_turkey.mp3",
            "fajr" => "adhan_fajr.mp3",
            _ => "adhan_makkah.mp3"
        };

        // 1. Cek di AppContext.BaseDirectory / Assets / Adhans
        string p1 = Path.Combine(AppContext.BaseDirectory, "Assets", "Adhans", fileName);
        if (File.Exists(p1)) return p1;

        // 2. Cek di %LOCALAPPDATA%\DRANOTCH\Adhans
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDataAdhans = Path.Combine(localAppData, "DRANOTCH", "Adhans");
        string p2 = Path.Combine(appDataAdhans, fileName);
        if (File.Exists(p2)) return p2;

        // 3. Cek di source code workspace (development fallback)
        try
        {
            string cur = AppDomain.CurrentDomain.BaseDirectory;
            var dirInfo = new DirectoryInfo(cur);
            while (dirInfo != null)
            {
                string devPath = Path.Combine(dirInfo.FullName, "src", "NotchMusic", "Assets", "Adhans", fileName);
                if (File.Exists(devPath))
                {
                    // Copy ke LocalAppData untuk caching
                    try
                    {
                        Directory.CreateDirectory(appDataAdhans);
                        File.Copy(devPath, p2, true);
                    }
                    catch { }
                    return devPath;
                }
                dirInfo = dirInfo.Parent;
            }
        }
        catch { }

        return null;
    }

    public static void PlayChime()
    {
        try
        {
            _cachedChimeWav ??= GenerateChimeWav();

            using var ms = new MemoryStream(_cachedChimeWav);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch { }
        }
    }

    public static void PlayDeadlineAlert()
    {
        try
        {
            _cachedDeadlineWav ??= GenerateDeadlineAlertWav();

            using var ms = new MemoryStream(_cachedDeadlineWav);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch
        {
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch { }
        }
    }

    private static byte[] GenerateChimeWav()
    {
        // 44100 Hz, 16-bit, mono, 0.9 seconds
        int sampleRate = 44100;
        double duration = 0.9;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            // Serene harmonic bell: D5 (587.33 Hz) + A5 (880 Hz) + D6 (1174.66 Hz)
            double envelope = Math.Exp(-3.5 * t);
            double wave = 0.60 * Math.Sin(2 * Math.PI * 587.33 * t)
                        + 0.30 * Math.Sin(2 * Math.PI * 880.00 * t)
                        + 0.10 * Math.Sin(2 * Math.PI * 1174.66 * t);
            samples[i] = (short)(wave * envelope * 26000);
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write(new char[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + numSamples * 2);
        bw.Write(new char[] { 'W', 'A', 'V', 'E' });

        // fmt subchunk
        bw.Write(new char[] { 'f', 'm', 't', ' ' });
        bw.Write(16); // Subchunk1Size (16 for PCM)
        bw.Write((short)1); // AudioFormat (1 for PCM)
        bw.Write((short)1); // NumChannels (1 for mono)
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // ByteRate
        bw.Write((short)2); // BlockAlign
        bw.Write((short)16); // BitsPerSample

        // data subchunk
        bw.Write(new char[] { 'd', 'a', 't', 'a' });
        bw.Write(numSamples * 2);
        for (int i = 0; i < numSamples; i++)
        {
            bw.Write(samples[i]);
        }

        return ms.ToArray();
    }

    private static byte[] GenerateDeadlineAlertWav()
    {
        // 44100 Hz, 16-bit, mono, 0.85 seconds
        int sampleRate = 44100;
        double duration = 0.85;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double wave = 0;

            if (t >= 0 && t < 0.5)
            {
                double env1 = Math.Exp(-8.0 * t);
                wave += env1 * (0.6 * Math.Sin(2 * Math.PI * 659.25 * t) + 0.3 * Math.Sin(2 * Math.PI * 1318.5 * t));
            }

            if (t >= 0.18)
            {
                double t2 = t - 0.18;
                double env2 = Math.Exp(-5.0 * t2);
                wave += env2 * (0.7 * Math.Sin(2 * Math.PI * 987.77 * t2) + 0.3 * Math.Sin(2 * Math.PI * 1975.53 * t2));
            }

            samples[i] = (short)(Math.Clamp(wave, -1.0, 1.0) * 26000);
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(new char[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + numSamples * 2);
        bw.Write(new char[] { 'W', 'A', 'V', 'E' });

        bw.Write(new char[] { 'f', 'm', 't', ' ' });
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);

        bw.Write(new char[] { 'd', 'a', 't', 'a' });
        bw.Write(numSamples * 2);
        for (int i = 0; i < numSamples; i++)
        {
            bw.Write(samples[i]);
        }

        return ms.ToArray();
    }
}
