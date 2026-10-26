using System;
using System.IO;
using System.Media;

namespace NotchMusic.Helpers;

public static class PrayerAudioHelper
{
    private static byte[]? _cachedChimeWav;
    private static byte[]? _cachedDeadlineWav;

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
        // Two-tone chime alert: note 1 (E5 659.25Hz) + note 2 (B5 987.77Hz / E6 1318.5Hz)
        int sampleRate = 44100;
        double duration = 0.85;
        int numSamples = (int)(sampleRate * duration);
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double wave = 0;

            // Note 1 starts at t = 0
            if (t >= 0 && t < 0.5)
            {
                double env1 = Math.Exp(-8.0 * t);
                wave += env1 * (0.6 * Math.Sin(2 * Math.PI * 659.25 * t) + 0.3 * Math.Sin(2 * Math.PI * 1318.5 * t));
            }

            // Note 2 starts at t = 0.18
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
