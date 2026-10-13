using UnityEngine;

namespace ColorPuzzle
{
    public static class GameAudioSettings
    {
        private const string MasterVolumePreference = "ColorPuzzle.MasterVolume";

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
    }
}
