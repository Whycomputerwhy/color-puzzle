using System;
using UnityEngine;

namespace ColorPuzzle
{
    [Serializable]
    public sealed class AudioCueSlot
    {
        [Tooltip("Leave empty until the final audio file is chosen. Multiple clips play as random variations.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 0.25f;
        [Range(0.5f, 2f)] public float minimumPitch = 1f;
        [Range(0.5f, 2f)] public float maximumPitch = 1f;
        [Tooltip("Use spatial playback for a sound that belongs at a world object.")]
        public bool spatial;
        [Tooltip("For ambience and sustained world sounds. The caller starts and stops these explicitly.")]
        public bool loop;
    }

    /// <summary>
    /// Project-wide, inspector-managed inventory of intended game audio. Add clips
    /// here once, then gameplay scripts can reuse the same named cue slot.
    /// </summary>
    [CreateAssetMenu(fileName = "GameAudioLibrary", menuName = "Color Puzzle/Audio Library")]
    public sealed class GameAudioLibrary : ScriptableObject
    {
        [Header("Player")]
        public AudioCueSlot footstepsSand = new() { volume = 0.12f };
        public AudioCueSlot jumpWater = new() { volume = 0.14f };
        public AudioCueSlot landingSand = new() { volume = 0.12f };

        [Header("Interaction")]
        public AudioCueSlot pickupSand = new() { volume = 0.18f };
        public AudioCueSlot dropSand = new() { volume = 0.16f };
        public AudioCueSlot magnifierOpen = new() { volume = 0.14f };
        public AudioCueSlot magnifierClose = new() { volume = 0.14f };

        [Header("Puzzle 1: Shell Keys")]
        [Tooltip("Puzzle 1's physical clam-casing opening, if it needs its own mechanical layer.")]
        public AudioCueSlot clamOpen = new() { volume = 0.20f };

        [Header("Puzzle 2: Fish")]
        public AudioCueSlot fishSwim = new() { volume = 0.08f, spatial = true, loop = true };

        [Header("Puzzle 3: Pattern Dials")]
        public AudioCueSlot seaweedFall = new() { volume = 0.18f, spatial = true };
        public AudioCueSlot turtleMove = new() { volume = 0.20f, spatial = true };

        [Header("Puzzle 4: Heart Dials")]
        public AudioCueSlot heartHum = new() { volume = 0.12f, spatial = true, loop = true };
        public AudioCueSlot heartProgress = new() { volume = 0.18f, spatial = true };

        [Header("Shared Puzzle")]
        public AudioCueSlot puzzleButton = new() { volume = 0.15f };
        public AudioCueSlot puzzleDial = new() { volume = 0.14f };
        [Tooltip("Plays when a full puzzle answer is accepted. Keep this short and readable.")]
        public AudioCueSlot puzzleCorrect = new() { volume = 0.24f };
        [Tooltip("Plays as the solved puzzle's own objects gain their persistent colour.")]
        public AudioCueSlot puzzleElementColorReveal = new() { volume = 0.20f };
        public AudioCueSlot puzzleFail = new() { volume = 0.22f };

        [Header("Finale")]
        [Tooltip("Plays only for the final expanding world-colour restoration, separate from ordinary puzzle success.")]
        public AudioCueSlot finalWorldColorReveal = new() { volume = 0.32f };

        [Header("World")]
        public AudioCueSlot oceanAmbience = new() { volume = 0.12f, loop = true };

        [Header("Layered Ocean Music")]
        [Tooltip("Always audible. All five music tracks must use the same exact loop length and start point.")]
        public AudioCueSlot oceanMusicBase = new() { volume = 0.12f, loop = true };
        [Tooltip("Fades in after Puzzle 1 is completed.")]
        public AudioCueSlot oceanMusicPuzzle1 = new() { volume = 0.12f, loop = true };
        [Tooltip("Fades in after Puzzle 2 is completed.")]
        public AudioCueSlot oceanMusicPuzzle2 = new() { volume = 0.12f, loop = true };
        [Tooltip("Fades in after Puzzle 3 is completed.")]
        public AudioCueSlot oceanMusicPuzzle3 = new() { volume = 0.12f, loop = true };
        [Tooltip("Fades in after Puzzle 4 is completed.")]
        public AudioCueSlot oceanMusicPuzzle4 = new() { volume = 0.12f, loop = true };

        [Header("UI")]
        public AudioCueSlot uiButton = new() { volume = 0.12f };
        public AudioCueSlot uiSlider = new() { volume = 0.08f };

        public AudioCueSlot GetOceanMusicLayer(int index)
        {
            return index switch
            {
                0 => oceanMusicBase,
                1 => oceanMusicPuzzle1,
                2 => oceanMusicPuzzle2,
                3 => oceanMusicPuzzle3,
                4 => oceanMusicPuzzle4,
                _ => null
            };
        }
    }
}
