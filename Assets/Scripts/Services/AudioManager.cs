using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Garden
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioMixer mixer;
        [SerializeField] private SoundLibrary library;

        private AudioSource _musicSource;
        private AudioSource[] _sfxSources;
        private int _sfxIndex;
        private Dictionary<string, SoundLibrary.SoundEntry> _lookup;

        private const int SfxPoolSize = 4;
        private const float MinDb = -80f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            if (mixer != null)
            {
                var musicGroup = mixer.FindMatchingGroups("Music");
                if (musicGroup.Length > 0) _musicSource.outputAudioMixerGroup = musicGroup[0];
            }

            _sfxSources = new AudioSource[SfxPoolSize];
            var sfxGroups = mixer != null ? mixer.FindMatchingGroups("SFX") : null;
            for (int i = 0; i < SfxPoolSize; i++)
            {
                _sfxSources[i] = gameObject.AddComponent<AudioSource>();
                _sfxSources[i].playOnAwake = false;
                if (sfxGroups != null && sfxGroups.Length > 0)
                    _sfxSources[i].outputAudioMixerGroup = sfxGroups[0];
            }

            BuildLookup();
        }

        private void Start()
        {
            var data = SaveManager.Instance?.Data;
            if (data != null)
            {
                SetMusicVolume(data.musicVolume);
                SetSFXVolume(data.sfxVolume);
            }

            PlayMusic();
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, SoundLibrary.SoundEntry>();
            if (library == null) return;
            foreach (var entry in library.entries)
            {
                if (!string.IsNullOrEmpty(entry.key))
                    _lookup[entry.key] = entry;
            }
        }

        public void PlaySFX(string key)
        {
            if (_lookup == null || !_lookup.TryGetValue(key, out var entry)) return;
            if (entry.clip == null) return;

            var source = _sfxSources[_sfxIndex];
            _sfxIndex = (_sfxIndex + 1) % SfxPoolSize;

            source.pitch = Random.Range(entry.pitchMin, entry.pitchMax);
            source.PlayOneShot(entry.clip, entry.volume);
        }

        public void PlaySFXWithFadeOut(string key, float fadeDuration)
        {
            if (_lookup == null || !_lookup.TryGetValue(key, out var entry)) return;
            if (entry.clip == null) return;

            var source = _sfxSources[_sfxIndex];
            _sfxIndex = (_sfxIndex + 1) % SfxPoolSize;

            source.Stop();
            source.clip = entry.clip;
            source.volume = entry.volume;
            source.pitch = Random.Range(entry.pitchMin, entry.pitchMax);
            source.Play();
            StartCoroutine(FadeOutSource(source, entry.volume, fadeDuration));
        }

        private IEnumerator FadeOutSource(AudioSource source, float startVolume, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && source.isPlaying)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }
            source.Stop();
            source.clip = null;
        }

        public void PlayMusic()
        {
            if (_lookup != null && _lookup.TryGetValue("music_main", out var entry) && entry.clip != null)
            {
                _musicSource.clip = entry.clip;
                _musicSource.volume = entry.volume;
                _musicSource.Play();
            }
        }

        public void StopMusic()
        {
            _musicSource.Stop();
        }

        public void SetMusicVolume(float volume01)
        {
            float db = volume01 > 0.0001f ? Mathf.Log10(volume01) * 20f : MinDb;
            if (mixer != null) mixer.SetFloat("MusicVolume", db);
        }

        public void SetSFXVolume(float volume01)
        {
            float db = volume01 > 0.0001f ? Mathf.Log10(volume01) * 20f : MinDb;
            if (mixer != null) mixer.SetFloat("SFXVolume", db);
        }
    }
}
