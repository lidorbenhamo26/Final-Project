using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AudioBus { Sfx, Voice, Music, Ambient }

/// <summary>
/// Centralized audio singleton. Loads clips from Resources/Audio/{bus}/<prefix>_<id>
/// on demand and caches them. Silently no-ops if a clip is missing so audio files
/// can land asynchronously from parallel content pipelines.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;

    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AudioManager");
                _instance = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private AudioSource _sfxSource;
    private AudioSource _voiceSource;
    private AudioSource _musicA;
    private AudioSource _musicB;
    private AudioSource _ambientSource;
    private bool _musicUsingA = true;

    private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<AudioBus, float> _busVolume = new Dictionary<AudioBus, float>
    {
        { AudioBus.Sfx, 1f },
        { AudioBus.Voice, 1f },
        { AudioBus.Music, 1f },
        { AudioBus.Ambient, 1f },
    };

    private Coroutine _musicFadeCo;
    private Coroutine _ambientFadeCo;
    private Coroutine _voiceDuckCo;
    private float _currentMusicTargetVolume; // pre-duck target on the active source

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;

        _voiceSource = gameObject.AddComponent<AudioSource>();
        _voiceSource.playOnAwake = false;
        _voiceSource.spatialBlend = 0f;

        _musicA = gameObject.AddComponent<AudioSource>();
        _musicA.playOnAwake = false;
        _musicA.spatialBlend = 0f;
        _musicA.loop = true;

        _musicB = gameObject.AddComponent<AudioSource>();
        _musicB.playOnAwake = false;
        _musicB.spatialBlend = 0f;
        _musicB.loop = true;

        _ambientSource = gameObject.AddComponent<AudioSource>();
        _ambientSource.playOnAwake = false;
        _ambientSource.spatialBlend = 0f;
        _ambientSource.loop = true;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public void PlaySfx(string id, float volume = 1f)
    {
        AudioClip clip = LoadClip(AudioBus.Sfx, id);
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * _busVolume[AudioBus.Sfx]);
    }

    public void PlayVoice(string id, float volume = 1f)
    {
        AudioClip clip = LoadClip(AudioBus.Voice, id);
        if (clip == null || _voiceSource == null) return;
        float v = Mathf.Clamp01(volume) * _busVolume[AudioBus.Voice];
        _voiceSource.PlayOneShot(clip, v);

        if (_voiceDuckCo != null) StopCoroutine(_voiceDuckCo);
        _voiceDuckCo = StartCoroutine(CoDuckMusicForVoice(clip.length));
    }

    public void PlayMusic(string id, float fade = 0.5f, bool loop = true)
    {
        AudioClip clip = LoadClip(AudioBus.Music, id);
        if (clip == null) return;

        AudioSource nextSource = _musicUsingA ? _musicB : _musicA;
        AudioSource prevSource = _musicUsingA ? _musicA : _musicB;

        nextSource.clip = clip;
        nextSource.loop = loop;
        nextSource.volume = 0f;
        nextSource.Play();

        _currentMusicTargetVolume = _busVolume[AudioBus.Music];

        if (_musicFadeCo != null) StopCoroutine(_musicFadeCo);
        _musicFadeCo = StartCoroutine(CoCrossfadeMusic(prevSource, nextSource, _currentMusicTargetVolume, fade));

        _musicUsingA = !_musicUsingA;
    }

    public void StopMusic(float fade = 0.5f)
    {
        if (_musicFadeCo != null) StopCoroutine(_musicFadeCo);
        _musicFadeCo = StartCoroutine(CoFadeOutMusic(fade));
        _currentMusicTargetVolume = 0f;
    }

    public void PlayAmbient(string id, float fade = 1f)
    {
        AudioClip clip = LoadClip(AudioBus.Ambient, id);
        if (clip == null || _ambientSource == null) return;

        if (_ambientFadeCo != null) StopCoroutine(_ambientFadeCo);
        _ambientFadeCo = StartCoroutine(CoSwapAmbient(clip, fade));
    }

    public void StopAmbient(float fade = 1f)
    {
        if (_ambientSource == null) return;
        if (_ambientFadeCo != null) StopCoroutine(_ambientFadeCo);
        _ambientFadeCo = StartCoroutine(CoFadeOutAmbient(fade));
    }

    public void SetBusVolume(AudioBus bus, float linear01)
    {
        float v = Mathf.Clamp01(linear01);
        _busVolume[bus] = v;

        if (bus == AudioBus.Music)
        {
            _currentMusicTargetVolume = v;
            AudioSource active = _musicUsingA ? _musicA : _musicB;
            if (active != null && active.isPlaying) active.volume = v;
        }
        else if (bus == AudioBus.Ambient)
        {
            if (_ambientSource != null && _ambientSource.isPlaying) _ambientSource.volume = v;
        }
    }

    private AudioClip LoadClip(AudioBus bus, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string path = BuildPath(bus, id);
        if (_cache.TryGetValue(path, out AudioClip cached)) return cached;

        AudioClip clip = Resources.Load<AudioClip>(path);
        // Cache the result either way (including null) so we don't keep hitting Resources.
        _cache[path] = clip;
        return clip;
    }

    private static string BuildPath(AudioBus bus, string id)
    {
        switch (bus)
        {
            case AudioBus.Sfx:     return "Audio/SFX/sfx_" + id;
            case AudioBus.Voice:   return "Audio/Voice/voice_" + id;
            case AudioBus.Music:   return "Audio/Music/music_" + id;
            case AudioBus.Ambient: return "Audio/Ambient/ambient_" + id;
        }
        return null;
    }

    private IEnumerator CoCrossfadeMusic(AudioSource from, AudioSource to, float targetVolume, float fade)
    {
        float startFromVol = from != null ? from.volume : 0f;
        if (fade <= 0f)
        {
            if (from != null) { from.volume = 0f; from.Stop(); }
            if (to != null)   to.volume = targetVolume;
            _musicFadeCo = null;
            yield break;
        }

        float t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fade);
            if (from != null) from.volume = Mathf.Lerp(startFromVol, 0f, k);
            if (to != null)   to.volume   = Mathf.Lerp(0f, targetVolume, k);
            yield return null;
        }
        if (from != null) { from.volume = 0f; from.Stop(); }
        if (to != null)   to.volume = targetVolume;
        _musicFadeCo = null;
    }

    private IEnumerator CoFadeOutMusic(float fade)
    {
        AudioSource active = _musicUsingA ? _musicA : _musicB;
        AudioSource other  = _musicUsingA ? _musicB : _musicA;
        float startA = active != null ? active.volume : 0f;
        float startB = other != null ? other.volume : 0f;
        if (fade <= 0f)
        {
            if (active != null) { active.volume = 0f; active.Stop(); }
            if (other != null)  { other.volume = 0f; other.Stop(); }
            _musicFadeCo = null;
            yield break;
        }

        float t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fade);
            if (active != null) active.volume = Mathf.Lerp(startA, 0f, k);
            if (other != null)  other.volume  = Mathf.Lerp(startB, 0f, k);
            yield return null;
        }
        if (active != null) { active.volume = 0f; active.Stop(); }
        if (other != null)  { other.volume = 0f; other.Stop(); }
        _musicFadeCo = null;
    }

    private IEnumerator CoSwapAmbient(AudioClip clip, float fade)
    {
        float targetVol = _busVolume[AudioBus.Ambient];
        if (_ambientSource.isPlaying && _ambientSource.clip != clip)
        {
            // fade out current first
            float fOut = Mathf.Max(0.01f, fade * 0.5f);
            float startVol = _ambientSource.volume;
            float t = 0f;
            while (t < fOut)
            {
                t += Time.unscaledDeltaTime;
                _ambientSource.volume = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(t / fOut));
                yield return null;
            }
            _ambientSource.Stop();
        }

        _ambientSource.clip = clip;
        _ambientSource.loop = true;
        _ambientSource.volume = 0f;
        _ambientSource.Play();

        float fIn = Mathf.Max(0.01f, fade);
        float t2 = 0f;
        while (t2 < fIn)
        {
            t2 += Time.unscaledDeltaTime;
            _ambientSource.volume = Mathf.Lerp(0f, targetVol, Mathf.Clamp01(t2 / fIn));
            yield return null;
        }
        _ambientSource.volume = targetVol;
        _ambientFadeCo = null;
    }

    private IEnumerator CoFadeOutAmbient(float fade)
    {
        float startVol = _ambientSource.volume;
        if (fade <= 0f)
        {
            _ambientSource.volume = 0f;
            _ambientSource.Stop();
            _ambientFadeCo = null;
            yield break;
        }
        float t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            _ambientSource.volume = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(t / fade));
            yield return null;
        }
        _ambientSource.volume = 0f;
        _ambientSource.Stop();
        _ambientFadeCo = null;
    }

    private IEnumerator CoDuckMusicForVoice(float duration)
    {
        AudioSource active = _musicUsingA ? _musicA : _musicB;
        if (active == null) { _voiceDuckCo = null; yield break; }

        float baseTarget = _currentMusicTargetVolume > 0f ? _currentMusicTargetVolume : _busVolume[AudioBus.Music];
        float ducked = baseTarget * 0.3f;
        active.volume = ducked;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            // Keep it ducked while voice plays.
            active.volume = ducked;
            yield return null;
        }
        // Restore (only if no fade is currently in progress).
        if (_musicFadeCo == null)
        {
            active.volume = baseTarget;
        }
        _voiceDuckCo = null;
    }
}
