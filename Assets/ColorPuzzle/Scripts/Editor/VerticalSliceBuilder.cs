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
        private const string StartupScenePath = "Assets/ColorPuzzle/Scenes/ColorPuzzleUnderwaterDemo.unity";
        private const string OriginalScenePath = "Assets/ColorPuzzle/Scenes/ColorPuzzleVerticalSlice.unity";

        [MenuItem("Color Puzzle/Validate Demo Build (Read Only)")]
        public static void PrepareDesktopBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StartupScenePath) == null)
                throw new FileNotFoundException("The underwater startup scene is missing. No files were changed.", StartupScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OriginalScenePath) == null)
                throw new FileNotFoundException("The original demo scene is missing. No files were changed.", OriginalScenePath);

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool valid = scenes.Length > 1 &&
                         scenes[0].enabled && scenes[0].path == StartupScenePath &&
                         scenes[1].enabled && scenes[1].path == OriginalScenePath;
            if (!valid)
                throw new System.InvalidOperationException(
                    "Build settings must open the underwater demo at index 0 and retain the original demo at index 1. No files were changed.");

            UnityEngine.Debug.Log(
                "COLOR PUZZLE BUILD VALID: underwater demo starts at index 0; original demo remains enabled at index 1. No files were changed.");
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
