using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>Synthesizes the kit's short feedback sounds as owned 16-bit WAV assets.</summary>
    static class WorkshopAudioFactory
    {
        const int SampleRate = 44100;

        public static AudioClip Pickup => Write("Pickup", Tones(0.07f, 0.5f, 988f, 1319f));
        public static AudioClip Respawn => Write("Respawn", Sweep(0.32f, 520f, 180f, 0.45f));
        public static AudioClip Win => Write("Win", Tones(0.12f, 0.45f, 523f, 659f, 784f, 1047f));
        public static AudioClip Lose => Write("Lose", Tones(0.18f, 0.45f, 392f, 330f, 262f));
        public static AudioClip Blocked => Write("Blocked", Square(0.16f, 170f, 0.25f));
        public static AudioClip Checkpoint => Write("Checkpoint", Tones(0.09f, 0.45f, 659f, 880f, 1175f));
        public static AudioClip Key => Write("Key", Tones(0.06f, 0.5f, 1319f, 1760f, 2093f));
        public static AudioClip Door => Write("Door", Sweep(0.35f, 160f, 320f, 0.35f));
        public static AudioClip Switch => Write("Switch", Tones(0.05f, 0.4f, 880f, 660f));

        static AudioClip Write(string name, float[] samples)
        {
            var path = $"{WorkshopPaths.Audio}/{name}.wav";
            var bytes = Encode(samples);
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || !SameBytes(File.ReadAllBytes(fullPath), bytes))
            {
                File.WriteAllBytes(fullPath, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        static float[] Tones(float noteSeconds, float volume, params float[] frequencies)
        {
            var noteLength = (int)(noteSeconds * SampleRate);
            var samples = new float[noteLength * frequencies.Length + SampleRate / 20];
            for (var n = 0; n < frequencies.Length; n++)
            for (var i = 0; i < noteLength; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Min(1f, i / 200f) * Mathf.Exp(-3.5f * i / noteLength);
                var wave = Mathf.Sin(2f * Mathf.PI * frequencies[n] * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * frequencies[n] * t);
                samples[n * noteLength + i] += wave * envelope * volume * 0.8f;
            }
            return samples;
        }

        static float[] Sweep(float seconds, float from, float to, float volume)
        {
            var length = (int)(seconds * SampleRate);
            var samples = new float[length];
            var phase = 0f;
            for (var i = 0; i < length; i++)
            {
                var progress = i / (float)length;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, progress) / SampleRate;
                var envelope = Mathf.Min(1f, i / 200f) * (1f - progress);
                samples[i] = Mathf.Sin(phase) * envelope * volume;
            }
            return samples;
        }

        static float[] Square(float seconds, float frequency, float volume)
        {
            var length = (int)(seconds * SampleRate);
            var samples = new float[length];
            for (var i = 0; i < length; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Min(1f, i / 200f) * (1f - i / (float)length);
                samples[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t)) * envelope * volume;
            }
            return samples;
        }

        static byte[] Encode(float[] samples)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            foreach (var sample in samples)
                writer.Write((short)Math.Round(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }
    }
}
