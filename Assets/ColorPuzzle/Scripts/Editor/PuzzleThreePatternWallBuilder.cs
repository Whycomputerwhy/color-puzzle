#if UNITY_EDITOR
using System;
using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    /// <summary>
    /// Places the authored sea-creature pattern models on Puzzle 3's existing wall.
    /// It deliberately edits neither the turtle/dials nor the stage instance layout.
    /// </summary>
    public static class PuzzleThreePatternWallBuilder
    {
        private const string BlockoutPrefabPath =
            "Assets/ColorPuzzle/Prefabs/Gameplay/TurtleDialLock/Turtle Dial Lock Blockout.prefab";
        private const string GreyMaterialPath =
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Architecture.mat";

        // WALL PATTERN PARAMETERS — ordered left to right on the existing wall.
        private static readonly PatternSpec[] Patterns =
        {
            new("Octopus", "Assets/ColorPuzzle/Models/Ocean/pattern_octopus.fbx",
                "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleBlue.mat", new Vector3(-4.35f, 2.25f, -0.39f)),
            new("Turtle", "Assets/ColorPuzzle/Models/Ocean/pattern_turtle.fbx",
                "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleYellow.mat", new Vector3(-1.45f, 2.25f, -0.39f)),
            new("Starfish", "Assets/ColorPuzzle/Models/Ocean/pattern_star.fbx",
                "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleRed.mat", new Vector3(1.45f, 2.25f, -0.39f)),
            new("Crab", "Assets/ColorPuzzle/Models/Ocean/pattern_crab.fbx",
                "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleGreen.mat", new Vector3(4.35f, 2.25f, -0.39f))
        };

        private const float TargetPatternSize = 1.55f;

        [MenuItem("Color Puzzle/Apply Puzzle 3 Wall Pattern Models (Preserve Dials)")]
        public static void Apply()
        {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(BlockoutPrefabPath);
            try
            {
                Transform wall = FindChild(prefabContents.transform, "Pattern Wall");
                if (wall == null)
                    throw new InvalidOperationException("Puzzle 3's Pattern Wall could not be found.");

                // Replace only the generated placeholder pattern panels. The wall,
                // dials, turtle, and their authored transforms remain untouched.
                RemoveChildrenNamed(wall, "Pattern Display");
                Transform oldPatterns = wall.Find("Imported Pattern Models");
                if (oldPatterns != null) UnityEngine.Object.DestroyImmediate(oldPatterns.gameObject);

                GameObject patternRoot = new GameObject("Imported Pattern Models");
                patternRoot.transform.SetParent(wall, false);
                Material grey = RequiredMaterial(GreyMaterialPath);
                foreach (PatternSpec spec in Patterns)
                    AddPattern(patternRoot.transform, spec, grey);

                PrefabUtility.SaveAsPrefabAsset(prefabContents, BlockoutPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Applied octopus, turtle, starfish, and crab models to Puzzle 3's Pattern Wall. Dials were preserved.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static void AddPattern(Transform parent, PatternSpec spec, Material grey)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.modelPath);
            if (source == null)
                throw new InvalidOperationException($"Missing pattern model: {spec.modelPath}");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            instance.name = $"Pattern {spec.name}";
            instance.transform.localPosition = spec.position;
            // The wall faces the room along its local negative Z direction.
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            instance.transform.localScale = Vector3.one;
            FitToWallPanel(instance, TargetPatternSize);

            Material color = RequiredMaterial(spec.colorMaterialPath);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                SetDualViewMaterials(renderer, grey, color);
        }

        private static void FitToWallPanel(GameObject instance, float targetSize)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) return;

            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y);
            if (largestDimension <= 0.0001f) return;
            instance.transform.localScale *= targetSize / largestDimension;
        }

        private static void SetDualViewMaterials(Renderer renderer, Material grey, Material color)
        {
            renderer.sharedMaterial = grey;
            DualViewMaterial dualView = renderer.GetComponent<DualViewMaterial>();
            if (dualView == null) dualView = renderer.gameObject.AddComponent<DualViewMaterial>();
            SerializedObject data = new SerializedObject(dualView);
            SetMaterial(data.FindProperty("greyscaleMaterials"), grey);
            SetMaterial(data.FindProperty("colorMaterials"), color);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMaterial(SerializedProperty materials, Material material)
        {
            materials.arraySize = 1;
            materials.GetArrayElementAtIndex(0).objectReferenceValue = material;
        }

        private static void RemoveChildrenNamed(Transform parent, string namePrefix)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child.name.StartsWith(namePrefix)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform result = FindChild(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static Material RequiredMaterial(string path) =>
            AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new InvalidOperationException($"Missing material: {path}");

        private readonly struct PatternSpec
        {
            public readonly string name;
            public readonly string modelPath;
            public readonly string colorMaterialPath;
            public readonly Vector3 position;

            public PatternSpec(string name, string modelPath, string colorMaterialPath, Vector3 position)
            {
                this.name = name;
                this.modelPath = modelPath;
                this.colorMaterialPath = colorMaterialPath;
                this.position = position;
            }
        }
    }
}
#endif
