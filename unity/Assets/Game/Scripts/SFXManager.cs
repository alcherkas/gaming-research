using UnityEngine;

/// <summary>
/// Handles procedural audio (SFX) generation at runtime.
/// Avoids the need for audio asset files.
/// </summary>
public static class SFXManager
{
    public static void PlaySwordSwing(Vector3 position)
    {
        int sampleRate = 44100;
        float duration = 0.1f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float noise = Random.Range(-1.0f, 1.0f);
            float envelope = Mathf.Exp(-t * 8f); // fast decay
            samples[i] = noise * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("SFX_Sword", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        AudioSource.PlayClipAtPoint(clip, position);
    }

    public static void PlaySpellCast(Vector3 position, DamageType type)
    {
        int sampleRate = 44100;
        float duration = 0.15f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float envelope = Mathf.Sin(t * Mathf.PI); // fade in and out

            if (type == DamageType.Fire)
            {
                // Sine sweep low to high (200Hz to 800Hz)
                float freq = Mathf.Lerp(200f, 800f, t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * (t * duration)) * envelope * 0.45f;
            }
            else if (type == DamageType.Ice)
            {
                // Sine sweep high to low (1000Hz to 300Hz)
                float freq = Mathf.Lerp(1000f, 300f, t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * (t * duration)) * envelope * 0.45f;
            }
            else if (type == DamageType.Lightning)
            {
                // Square wave with frequency jitter (buzzing sound)
                float baseFreq = 400f;
                float jitter = Random.Range(-80f, 80f);
                float freq = baseFreq + jitter;
                float val = Mathf.Sin(2f * Mathf.PI * freq * (t * duration));
                samples[i] = (val >= 0f ? 1f : -1f) * envelope * 0.22f;
            }
            else if (type == DamageType.Poison)
            {
                // White noise burst mixed with bubble sine wave
                float noise = Random.Range(-1.0f, 1.0f) * 0.35f;
                float bubble = Mathf.Sin(2f * Mathf.PI * 40f * (t * duration)) * 0.65f;
                samples[i] = (noise + bubble) * envelope * 0.35f;
            }
            else
            {
                samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * (t * duration)) * envelope * 0.45f;
            }
        }

        AudioClip clip = AudioClip.Create("SFX_Cast_" + type.ToString(), sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        AudioSource.PlayClipAtPoint(clip, position);
    }

    public static void PlayHit(Vector3 position)
    {
        int sampleRate = 44100;
        float duration = 0.08f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float val = Mathf.Sin(2f * Mathf.PI * 160f * (t * duration));
            float square = val >= 0f ? 1f : -1f;
            float envelope = Mathf.Exp(-t * 11f); // rapid decay
            samples[i] = square * envelope * 0.3f;
        }

        AudioClip clip = AudioClip.Create("SFX_Hit", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        AudioSource.PlayClipAtPoint(clip, position);
    }

    public static void PlayDash(Vector3 position)
    {
        int sampleRate = 44100;
        float duration = 0.12f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float noise = Random.Range(-1.0f, 1.0f);
            float envelope = Mathf.Sin(t * Mathf.PI); // fade in/out whoosh
            samples[i] = noise * envelope * 0.25f;
        }

        AudioClip clip = AudioClip.Create("SFX_Dash", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        AudioSource.PlayClipAtPoint(clip, position);
    }

    public static void PlayPickup(Vector3 position)
    {
        int sampleRate = 44100;
        float duration = 0.2f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float envelope = Mathf.Sin(t * Mathf.PI);

            // C5 (523.25Hz), E5 (659.25Hz), G5 (783.99Hz) rising arpeggio
            float freq;
            if (t < 0.33f) freq = 523.25f;
            else if (t < 0.66f) freq = 659.25f;
            else freq = 783.99f;

            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * (t * duration)) * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("SFX_Pickup", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        AudioSource.PlayClipAtPoint(clip, position);
    }
}
