using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates short non-speech ambience clips entirely in code (no audio assets):
/// robotic blips, radio static, sonar pings, alien chirps and soft warning beeps.
/// Used by <see cref="AmbienceDirector"/> so the ship has a live soundscape of
/// background distraction even though no recorded VO/SFX files ship with the
/// project. Clips are generated once and cached.
/// </summary>
public static class ProceduralSfx
{
    public enum Kind { Blip, Static, Ping, Chirp, Beep, Hum }

    private const int SampleRate = 44100;
    private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
    // Deterministic noise so generation never depends on Random (which is fine
    // here, but a fixed seed keeps the static texture stable run to run).
    private static uint noiseState = 0x9E3779B9u;

    /// <summary>Returns a cached clip for the given kind/variant. Variant lets the
    /// caller request a few distinct flavors of the same family (e.g. blip 0/1/2).</summary>
    public static AudioClip Get(Kind kind, int variant = 0)
    {
        string key = kind + ":" + variant;
        if (Cache.TryGetValue(key, out var c) && c != null) return c;
        c = Build(kind, variant);
        Cache[key] = c;
        return c;
    }

    private static AudioClip Build(Kind kind, int variant)
    {
        switch (kind)
        {
            case Kind.Blip:   return BuildBlip(variant);
            case Kind.Static: return BuildStatic(variant);
            case Kind.Ping:   return BuildPing(variant);
            case Kind.Chirp:  return BuildChirp(variant);
            case Kind.Beep:   return BuildBeep(variant);
            case Kind.Hum:    return BuildHum(variant);
        }
        return BuildBlip(variant);
    }

    // Two short stepped tones — a small "computer acknowledges" blip.
    private static AudioClip BuildBlip(int variant)
    {
        float f1 = 760f + variant * 90f;
        float f2 = f1 * 1.5f;
        float seg = 0.06f;
        int n = Mathf.RoundToInt(SampleRate * seg * 2f);
        var s = new float[n];
        int half = n / 2;
        for (int i = 0; i < n; i++)
        {
            float f = i < half ? f1 : f2;
            float local = (i < half ? i : i - half) / (float)half;
            float env = Envelope(local, 0.15f, 0.3f);
            s[i] = Mathf.Sin(2f * Mathf.PI * f * (i / (float)SampleRate)) * env * 0.5f;
        }
        return Make("blip" + variant, s);
    }

    // Band-ish radio static burst with a soft envelope.
    private static AudioClip BuildStatic(int variant)
    {
        float dur = 0.22f + variant * 0.05f;
        int n = Mathf.RoundToInt(SampleRate * dur);
        var s = new float[n];
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float local = i / (float)n;
            float env = Envelope(local, 0.1f, 0.5f);
            // Low-pass the white noise a touch so it reads as comms hiss, not click.
            float white = Noise();
            prev = Mathf.Lerp(prev, white, 0.5f);
            s[i] = prev * env * 0.35f;
        }
        return Make("static" + variant, s);
    }

    // Single decaying tone — a sonar/console ping.
    private static AudioClip BuildPing(int variant)
    {
        float f = 1100f + variant * 160f;
        float dur = 0.4f;
        int n = Mathf.RoundToInt(SampleRate * dur);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            float local = i / (float)n;
            float env = Mathf.Exp(-local * 6f); // sharp attack, long-ish tail
            s[i] = Mathf.Sin(2f * Mathf.PI * f * (i / (float)SampleRate)) * env * 0.45f;
        }
        return Make("ping" + variant, s);
    }

    // Warbling frequency sweep with vibrato — "alien interference".
    private static AudioClip BuildChirp(int variant)
    {
        float dur = 0.5f;
        int n = Mathf.RoundToInt(SampleRate * dur);
        var s = new float[n];
        float baseF = 520f + variant * 120f;
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float local = i / (float)n;
            // Sweep up then down, with a fast vibrato on top.
            float sweep = Mathf.Sin(local * Mathf.PI);          // 0..1..0
            float vib = 1f + 0.12f * Mathf.Sin(local * 60f);
            float f = (baseF + 700f * sweep) * vib;
            phase += 2f * Mathf.PI * f / SampleRate;
            float env = Envelope(local, 0.12f, 0.4f);
            s[i] = Mathf.Sin(phase) * env * 0.35f;
        }
        return Make("chirp" + variant, s);
    }

    // Soft double beep — a non-critical caution tone.
    private static AudioClip BuildBeep(int variant)
    {
        float f = 430f + variant * 70f;
        float beep = 0.1f, gap = 0.06f;
        int nb = Mathf.RoundToInt(SampleRate * beep);
        int ng = Mathf.RoundToInt(SampleRate * gap);
        int n = nb * 2 + ng;
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            int seg = i < nb ? 0 : (i < nb + ng ? 1 : 2);
            if (seg == 1) { s[i] = 0f; continue; }
            int li = seg == 0 ? i : i - nb - ng;
            float local = li / (float)nb;
            float env = Envelope(local, 0.1f, 0.2f);
            // Slightly square for an electronic edge.
            float t = f * (i / (float)SampleRate);
            float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * t)) * 0.4f + Mathf.Sin(2f * Mathf.PI * t) * 0.6f;
            s[i] = sq * env * 0.4f;
        }
        return Make("beep" + variant, s);
    }

    // Very short low hum swell — distant machinery.
    private static AudioClip BuildHum(int variant)
    {
        float f = 90f + variant * 25f;
        float dur = 0.7f;
        int n = Mathf.RoundToInt(SampleRate * dur);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            float local = i / (float)n;
            float env = Envelope(local, 0.3f, 0.4f);
            float tone = Mathf.Sin(2f * Mathf.PI * f * (i / (float)SampleRate))
                       + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 2f * (i / (float)SampleRate));
            s[i] = tone * env * 0.22f;
        }
        return Make("hum" + variant, s);
    }

    // Attack/decay envelope over a normalized 0..1 position.
    private static float Envelope(float local, float attack, float release)
    {
        float a = attack <= 0f ? 1f : Mathf.Clamp01(local / attack);
        float r = release <= 0f ? 1f : Mathf.Clamp01((1f - local) / release);
        return a * r;
    }

    // Cheap deterministic white noise in [-1, 1].
    private static float Noise()
    {
        noiseState ^= noiseState << 13;
        noiseState ^= noiseState >> 17;
        noiseState ^= noiseState << 5;
        return (noiseState / (float)uint.MaxValue) * 2f - 1f;
    }

    private static AudioClip Make(string name, float[] samples)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
