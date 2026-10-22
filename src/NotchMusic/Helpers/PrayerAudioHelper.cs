using System;
using System.IO;
using System.Media;

namespace NotchMusic.Helpers;

public static class PrayerAudioHelper
{
    private static byte[]? _cachedChimeWav;

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
}
