#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace ColorPuzzle.Editor
{
    /// <summary>
    /// Read-only validation for the ongoing authored project.
    /// The original scene/prefab/material generator was intentionally removed after initial setup.
    /// </summary>
    public static class VerticalSliceBuilder
    {
        private const string StartMenuScenePath = "Assets/ColorPuzzle/Scenes/ColorPuzzleStartMenu.unity";
        private const string ForestScenePath = "Assets/ColorPuzzle/Scenes/ColorPuzzleForestTutorial.unity";
        private const string OceanScenePath = "Assets/ColorPuzzle/Scenes/ColorPuzzleUnderwaterDemo.unity";
        private const string VillageScenePath = "Assets/ColorPuzzle/Scenes/ColorPuzzleVillageEnding.unity";

        [MenuItem("Color Puzzle/Validate Alpha Build (Read Only)")]
        public static void PrepareDesktopBuild()
        {
            ValidateSceneExists(StartMenuScenePath, "start menu");
            ValidateSceneExists(ForestScenePath, "forest tutorial");
            ValidateSceneExists(OceanScenePath, "ocean level");
            ValidateSceneExists(VillageScenePath, "village ending");

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool valid = scenes.Length == 4 &&
                         scenes[0].enabled && scenes[0].path == StartMenuScenePath &&
                         scenes[1].enabled && scenes[1].path == ForestScenePath &&
                         scenes[2].enabled && scenes[2].path == OceanScenePath &&
                         scenes[3].enabled && scenes[3].path == VillageScenePath;
            if (!valid)
                throw new System.InvalidOperationException(
                    "Build settings must contain Start Menu, Forest, Ocean, and Village at indices 0, 1, 2, and 3. No files were changed.");

            UnityEngine.Debug.Log(
                "COLOR PUZZLE ALPHA BUILD VALID: Start Menu -> Forest -> Ocean -> Village. No files were changed.");
        }

        private static void ValidateSceneExists(string scenePath, string levelLabel)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new FileNotFoundException($"The {levelLabel} scene is missing. No files were changed.", scenePath);
        }

        /// <summary>Compatibility guard for old automation.</summary>
        public static void Build()
        {
            throw new System.InvalidOperationException(
                "The destructive vertical-slice generator was permanently removed because this project contains manually authored work. No files were changed.");
        }

        /// <summary>Compatibility guard for old validation automation.</summary>
        public static void ValidateAndCapture()
        {
            PrepareDesktopBuild();
        }
    }
}
#endif
