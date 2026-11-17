using UnityEngine;

namespace ColorPuzzle
{
    public static class GameAudioSettings
    {
        private const string MasterVolumePreference = "ColorPuzzle.MasterVolume";
        private const string MusicVolumePreference = "ColorPuzzle.MusicVolume";
        private const string SfxVolumePreference = "ColorPuzzle.SfxVolume";
        private const string UiVolumePreference = "ColorPuzzle.UiVolume";

        public static float LoadMasterVolume(float fallback)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePreference, fallback));
        }

        public static void SetMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterVolumePreference, AudioListener.volume);
            PlayerPrefs.Save();
        }

        public static float LoadMusicVolume(float fallback) => LoadVolume(MusicVolumePreference, fallback);
        public static float LoadSfxVolume(float fallback) => LoadVolume(SfxVolumePreference, fallback);
        public static float LoadUiVolume(float fallback) => LoadVolume(UiVolumePreference, fallback);

        public static void SetMusicVolume(float volume) => SaveVolume(MusicVolumePreference, volume);
        public static void SetSfxVolume(float volume) => SaveVolume(SfxVolumePreference, volume);
        public static void SetUiVolume(float volume) => SaveVolume(UiVolumePreference, volume);

        private static float LoadVolume(string preference, float fallback)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(preference, fallback));
        }

        private static void SaveVolume(string preference, float volume)
        {
            PlayerPrefs.SetFloat(preference, Mathf.Clamp01(volume));
            PlayerPrefs.Save();
        }
    }
}
