using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>
    /// Persistent owner for shared audio playback and category volume controls.
    /// The cue inventory itself lives in Resources/Audio/GameAudioLibrary.asset.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GameAudioMaster : MonoBehaviour
    {
        private const string LibraryPath = "Audio/GameAudioLibrary";
        private static GameAudioMaster instance;

        [SerializeField] private GameAudioLibrary library;
        // Cue slots own the authored music balance. The category starts at full
        // scale so we do not multiply that quiet authored value a second time.
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float defaultUiVolume = 0.8f;
        [Header("Output")]
        [Tooltip("Shared gain for all master-managed audio. 1.5 is 50% louder than the authored cue volumes.")]
        [SerializeField, Range(0f, 2f)] private float outputGain = 1.5f;

        private const int OceanMusicLayerCount = 5;
        private readonly AudioSource[] musicSources = new AudioSource[OceanMusicLayerCount];
        private readonly float[] musicLayerMix = new float[OceanMusicLayerCount];
        private readonly Coroutine[] musicLayerFades = new Coroutine[OceanMusicLayerCount];
        private AudioSource sfxSource;
        private AudioSource uiSource;
        private AudioSource ambienceSource;
        private bool layeredMusicStarted;

        public static GameAudioMaster Instance => instance;
        public GameAudioLibrary Library => library;
        public float SfxOutputVolume => Mathf.Clamp01(GameAudioSettings.LoadSfxVolume(defaultSfxVolume) * outputGain);
        public bool IsLayeredOceanMusicPlaying => layeredMusicStarted && musicSources[0] != null && musicSources[0].isPlaying;
        public bool HasLayeredOceanMusicConfigured
        {
            get
            {
                AudioCueSlot baseLayer = library != null ? library.GetOceanMusicLayer(0) : null;
                return baseLayer != null && baseLayer.clips != null && baseLayer.clips.Length > 0 &&
                       baseLayer.clips[0] != null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateForPlaySession()
        {
            if (instance != null) return;
            GameObject root = new GameObject("GameAudioMaster");
            root.AddComponent<GameAudioMaster>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            library ??= Resources.Load<GameAudioLibrary>(LibraryPath);
            AudioListener.pause = false;
            // A previously saved mute setting can otherwise make every newly
            // added source appear broken. The pause/menu slider can still lower
            // this later; only a stale full mute is recovered here.
            if (AudioListener.volume <= 0.001f)
                GameAudioSettings.SetMasterVolume(0.85f);
            for (int i = 0; i < OceanMusicLayerCount; i++)
                musicSources[i] = CreateSource($"Music Layer {i + 1}", 0f);
            sfxSource = CreateSource("SFX", 0f);
            uiSource = CreateSource("UI", 0f);
            ambienceSource = CreateSource("Ocean Ambience", 0f);
            ApplySavedVolumes();
        }

        private void Start()
        {
            // Existing scenes keep their legacy soundtrack until a base layer is
            // deliberately assigned in GameAudioLibrary.
            if (HasLayeredOceanMusicConfigured)
                StartLayeredOceanMusic();
            StartOceanAmbience();
        }

        public void ApplySavedVolumes()
        {
            RefreshMusicVolumes();
            if (sfxSource != null) sfxSource.volume = SfxOutputVolume;
            if (uiSource != null) uiSource.volume = Mathf.Clamp01(GameAudioSettings.LoadUiVolume(defaultUiVolume) * outputGain);
        }

        public bool PlaySfx(AudioCueSlot cue) => Play(sfxSource, cue);
        public bool PlayUi(AudioCueSlot cue) => Play(uiSource, cue);

        public void PlayMusic(AudioCueSlot cue)
        {
            AudioSource musicSource = musicSources[0];
            if (musicSource == null || cue == null || cue.clips == null || cue.clips.Length == 0) return;
            AudioClip clip = ChooseClip(cue);
            if (clip == null) return;
            musicSource.clip = clip;
            musicSource.loop = cue.loop;
            musicSource.pitch = Random.Range(cue.minimumPitch, cue.maximumPitch);
            musicSource.Play();
        }

        /// <summary>
        /// Starts the base and four progression tracks on one DSP timestamp.
        /// Layers with no assigned clip are simply skipped until authored.
        /// </summary>
        public void StartLayeredOceanMusic()
        {
            if (layeredMusicStarted) return;
            if (!HasLayeredOceanMusicConfigured)
            {
                Debug.LogError("Layered ocean music could not start: GameAudioLibrary or Ocean Music Base has no assigned clip.", this);
                return;
            }

            // All stems begin on one audio-DSP timestamp, so they remain aligned
            // for their entire matching loop duration rather than merely starting
            // in the same rendered frame.
            double synchronizedStartTime = AudioSettings.dspTime + 0.15d;
            for (int i = 0; i < OceanMusicLayerCount; i++)
            {
                AudioCueSlot cue = library.GetOceanMusicLayer(i);
                AudioSource source = musicSources[i];
                if (source == null || cue == null || cue.clips == null || cue.clips.Length == 0) continue;

                source.clip = cue.clips[0];
                source.loop = true;
                source.pitch = 1f;
                musicLayerMix[i] = i == 0 ? 1f : 0f;
                source.timeSamples = 0;
                source.PlayScheduled(synchronizedStartTime);
            }

            layeredMusicStarted = true;
            RefreshMusicVolumes();
        }

        /// <summary>Sets the mix for one puzzle layer: 1 through 4 after its puzzle completes.</summary>
        public void SetOceanMusicPuzzleCompleted(int puzzleNumber, bool completed, float fadeDuration = 0.65f)
        {
            if (puzzleNumber < 1 || puzzleNumber >= OceanMusicLayerCount) return;
            StartLayeredOceanMusic();
            SetOceanMusicLayerMix(puzzleNumber, completed ? 1f : 0f, fadeDuration);
        }

        public void SetOceanMusicLayerMix(int layerIndex, float targetMix, float fadeDuration = 0.65f)
        {
            if (layerIndex < 0 || layerIndex >= OceanMusicLayerCount) return;
            if (musicLayerFades[layerIndex] != null) StopCoroutine(musicLayerFades[layerIndex]);
            musicLayerFades[layerIndex] = StartCoroutine(FadeOceanMusicLayer(
                layerIndex, Mathf.Clamp01(targetMix), Mathf.Max(0f, fadeDuration)));
        }

        public void StopMusic()
        {
            foreach (AudioSource source in musicSources)
                if (source != null) source.Stop();
            layeredMusicStarted = false;
        }

        private void StartOceanAmbience()
        {
            AudioCueSlot cue = library != null ? library.oceanAmbience : null;
            if (ambienceSource == null || cue == null || cue.clips == null || cue.clips.Length == 0) return;
            ambienceSource.clip = cue.clips[0];
            ambienceSource.loop = true;
            ambienceSource.volume = SfxOutputVolume * cue.volume;
            ambienceSource.Play();
        }

        private System.Collections.IEnumerator FadeOceanMusicLayer(int layerIndex, float targetMix, float duration)
        {
            float initialMix = musicLayerMix[layerIndex];
            if (duration <= 0f)
            {
                musicLayerMix[layerIndex] = targetMix;
                RefreshMusicVolumes();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicLayerMix[layerIndex] = Mathf.Lerp(initialMix, targetMix, elapsed / duration);
                RefreshMusicVolumes();
                yield return null;
            }

            musicLayerMix[layerIndex] = targetMix;
            RefreshMusicVolumes();
            musicLayerFades[layerIndex] = null;
        }

        private void RefreshMusicVolumes()
        {
            float categoryVolume = outputGain;
            for (int i = 0; i < OceanMusicLayerCount; i++)
            {
                AudioSource source = musicSources[i];
                AudioCueSlot cue = library != null ? library.GetOceanMusicLayer(i) : null;
                if (source != null)
                    source.volume = categoryVolume * musicLayerMix[i] * (cue != null ? cue.volume : 1f);
            }
        }

        private static bool Play(AudioSource source, AudioCueSlot cue)
        {
            if (source == null || cue == null || cue.loop || cue.clips == null || cue.clips.Length == 0) return false;
            AudioClip clip = ChooseClip(cue);
            if (clip == null) return false;
            source.pitch = Random.Range(cue.minimumPitch, cue.maximumPitch);
            source.PlayOneShot(clip, cue.volume);
            return true;
        }

        private AudioSource CreateSource(string sourceName, float spatialBlend)
        {
            GameObject sourceObject = new GameObject(sourceName, typeof(AudioSource));
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.ignoreListenerPause = sourceName.StartsWith("Music Layer");
            return source;
        }

        private static AudioClip ChooseClip(AudioCueSlot cue)
        {
            if (cue.clips == null || cue.clips.Length == 0) return null;
            return cue.clips[Random.Range(0, cue.clips.Length)];
        }
    }
}
