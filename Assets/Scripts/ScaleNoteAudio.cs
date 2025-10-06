using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

public sealed class ScaleNoteAudio : MonoBehaviour
{
    public AudioClip[] degreeClips = new AudioClip[7];
    public AudioClip[] sustainClips = new AudioClip[7];

    public int rootMidi = 60;
    public float oneShotLenSec = 0.14f;
    public float attackSec = 0.004f, releaseSec = 0.10f;
    public float sustainLoopSeconds = 1.0f;
    public bool playOneShotOnSustainStart = false;

    public int polyphony = 8;
    public AudioMixerGroup output;
    [Range(0,1)] public float volume = 0.9f;

    AudioSource[] oneShots; int oneIx;
    AudioSource[] sustains;
    AudioClip[] runtimeOne;
    AudioClip[] runtimeLoop;
    Coroutine[] fadeCo = new Coroutine[7];
    static readonly int[] MajorSemis = {0,2,4,5,7,9,11};

    void Awake()
    {
        oneShots = new AudioSource[Mathf.Max(1, polyphony)];
        for (int i=0;i<oneShots.Length;i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; 
            s.spatialBlend = 0f; // 2D audio
            s.outputAudioMixerGroup = output; 
            s.volume = volume;
            s.pitch = 1.0f; // Ensure consistent pitch
            s.priority = 128; // Normal priority
            s.bypassEffects = true; // Bypass audio effects to prevent artifacts
            s.bypassListenerEffects = true;
            s.bypassReverbZones = true;
            oneShots[i] = s;
        }
        sustains = new AudioSource[7];
        for (int i=0;i<7;i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; 
            s.loop = false; 
            s.spatialBlend = 0f; // 2D audio
            s.outputAudioMixerGroup = output; 
            s.volume = 0f;
            s.pitch = 1.0f; // Ensure consistent pitch
            s.priority = 128; // Normal priority
            s.bypassEffects = true; // Bypass audio effects to prevent artifacts
            s.bypassListenerEffects = true;
            s.bypassReverbZones = true;
            sustains[i] = s;
        }

        bool needOne=false, needLoop=false;
        for (int i=0;i<7;i++) if (degreeClips == null || i >= degreeClips.Length || degreeClips[i]==null) needOne = true;
        for (int i=0;i<7;i++) if (sustainClips == null || i >= sustainClips.Length || sustainClips[i]==null) needLoop = true;
        if (needOne || needLoop)
        {
            int sr = AudioSettings.outputSampleRate;
            runtimeOne = new AudioClip[7];
            runtimeLoop = new AudioClip[7];
            for (int d=0; d<7; d++)
            {
                int midi = rootMidi + MajorSemis[d];
                float f = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
                if (needOne)  runtimeOne[d]  = CreateToneClip(f, oneShotLenSec, attackSec, releaseSec, sr);
                if (needLoop) runtimeLoop[d] = CreateSeamlessLoop(f, sustainLoopSeconds, sr);
            }
        }
    }

    public void PlayDegree(int degree, double scheduleDsp = 0)
    {
        int idx = Mathf.Clamp(degree - 1, 0, 6);
        var clip = (degreeClips != null && degreeClips.Length > idx && degreeClips[idx] != null) ? degreeClips[idx] : runtimeOne[idx];
        var src = oneShots[oneIx]; oneIx = (oneIx + 1) % oneShots.Length;
        
        // Stop any currently playing audio to prevent overlapping
        if (src.isPlaying) src.Stop();
        
        // Ensure consistent settings for all audio sources
        src.clip = clip;
        src.volume = volume;
        src.pitch = 1.0f;
        src.priority = 128;
        src.bypassEffects = true;
        src.bypassListenerEffects = true;
        src.bypassReverbZones = true;
        src.spatialBlend = 0f; // 2D audio
        src.outputAudioMixerGroup = output;
        
        if (scheduleDsp > 0) src.PlayScheduled(scheduleDsp);
        else src.Play();
    }

    public void SustainStart(int degree, float fadeMs = 20f)
    {
        int idx = Mathf.Clamp(degree - 1, 0, 6);
        var clip = (sustainClips != null && sustainClips.Length > idx && sustainClips[idx] != null) ? sustainClips[idx] : runtimeLoop[idx];
        var s = sustains[idx];

        if (!s.isPlaying || s.clip != clip)
        {
            s.clip = clip;
            s.volume = 0f;
            s.Play();
            if (playOneShotOnSustainStart) PlayDegree(degree);
        }
        if (fadeCo[idx] != null) StopCoroutine(fadeCo[idx]);
        fadeCo[idx] = StartCoroutine(FadeVolume(s, s.volume, volume, fadeMs/1000f));
    }

    public void SustainStop(int degree, float fadeMs = 25f)
    {
        int idx = Mathf.Clamp(degree - 1, 0, 6);
        var s = sustains[idx];
        if (!s.isPlaying) return;
        if (fadeCo[idx] != null) StopCoroutine(fadeCo[idx]);
        fadeCo[idx] = StartCoroutine(FadeOutAndStop(s, fadeMs/1000f));
    }

    static IEnumerator FadeVolume(AudioSource s, float from, float to, float sec)
    {
        if (sec <= 0f) { s.volume = to; yield break; }
        float t=0f;
        while (t < sec && s)
        {
            t += Time.unscaledDeltaTime;
            s.volume = Mathf.Lerp(from, to, t/sec);
            yield return null;
        }
        if (s) s.volume = to;
    }

    static IEnumerator FadeOutAndStop(AudioSource s, float sec)
    {
        if (!s) yield break;
        float start = s.volume;
        float t=0f;
        while (t < sec && s)
        {
            t += Time.unscaledDeltaTime;
            s.volume = Mathf.Lerp(start, 0f, t/sec);
            yield return null;
        }
        if (s)
        {
            s.volume = 0f;
            s.Stop();
        }
    }

    static AudioClip CreateToneClip(float f, float len, float atk, float rel, int sr)
    {
        int n = Mathf.CeilToInt(len * sr);
        var data = new float[n];
        float phase = 0f;
        float step = 2f*Mathf.PI*f/sr;
        for (int i=0;i<n;i++)
        {
            float t = i / (float)sr;
            float s = Mathf.Sin(phase) + 0.25f*Mathf.Sin(2*phase) + 0.12f*Mathf.Sin(3*phase);
            phase += step;
            float a = Mathf.Clamp01(t/Mathf.Max(0.0001f, atk));
            float r = Mathf.Clamp01((len-t)/Mathf.Max(0.0001f, rel));
            float e = Mathf.Min(a, r);
            data[i] = s * e * 0.5f;
        }
        var clip = AudioClip.Create("One_"+f.ToString("0.0"), n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateSeamlessLoop(float f, float seconds, int sr)
    {
        int n = Mathf.CeilToInt(seconds * sr);
        var data = new float[n];
        float phase = 0f;
        float step = 2f*Mathf.PI*f/sr;
        for (int i=0;i<n;i++)
        {
            data[i] = 0.6f*Mathf.Sin(phase) + 0.15f*Mathf.Sin(2*phase);
            phase += step;
        }
        int X = Mathf.Clamp(n/16, 256, 4096);
        for (int i=0;i<X;i++)
        {
            float w = i/(float)X;
            int tail = n - X + i;
            data[tail] = data[tail]*(1f - w) + data[i]*w;
        }
        var clip = AudioClip.Create("Loop_"+f.ToString("0.0"), n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
