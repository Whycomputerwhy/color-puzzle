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
        private const string DemoScenePath = "Assets/ColorPuzzle/Scenes/ColorPuzzleVerticalSlice.unity";

        [MenuItem("Color Puzzle/Validate Demo Build (Read Only)")]
        public static void PrepareDesktopBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
                throw new FileNotFoundException("The Color Puzzle demo scene is missing. No files were changed.", DemoScenePath);

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool valid = scenes.Length > 0 && scenes[0].enabled && scenes[0].path == DemoScenePath;
            if (!valid)
                throw new System.InvalidOperationException(
                    "Build settings are not configured to open the Color Puzzle demo at index 0. No files were changed.");

            UnityEngine.Debug.Log(
                "COLOR PUZZLE BUILD VALID: demo scene is enabled at build index 0. No files were changed.");
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
