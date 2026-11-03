#if UNITY_EDITOR
using System;
using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    /// <summary>Creates a non-destructive, editable blockout for Puzzle 3.</summary>
    public static class TurtleDialLockBlockoutBuilder
    {
        private const string StagePath = "Assets/ColorPuzzle/Prefabs/Level/CampaignStages/OceanStage03.prefab";
        private const string PrefabFolder = "Assets/ColorPuzzle/Prefabs/Gameplay/TurtleDialLock";
        private const string PrefabPath = PrefabFolder + "/Turtle Dial Lock Blockout.prefab";
        private const string DialBasePrefabPath = PrefabFolder + "/Number Dial Base.prefab";
        private const string DialVariantPathFormat = PrefabFolder + "/Number Dial {0:00}.prefab";

        private const string GreyStone = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Stone.mat";
        private const string GreyArchitecture = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Architecture.mat";
        private const string Aqua = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_Aqua.mat";
        private const string DeepBlue = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_DeepBlue.mat";
        private const string Gold = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_TreasureGold.mat";
        private const string WrongCombinationClip = "Assets/ColorPuzzle/Resources/Audio/WrongShellPlacement.wav";
        private const string CorrectCombinationClip = "Assets/ColorPuzzle/Resources/Audio/CorrectShellPlacementAndReveal.wav";
        private static readonly int[] DialTargets = { 8, 4, 5, 10 };
        private static readonly string[] PatternMaterials =
        {
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleBlue.mat",
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleYellow.mat",
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleRed.mat",
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleGreen.mat"
        };

        [MenuItem("Color Puzzle/Create Puzzle 3 Turtle Dial Lock Blockout")]
        public static void Create()
        {
            GameObject blockoutPrefab = GetOrCreateBlockoutPrefab();
            GameObject stage = PrefabUtility.LoadPrefabContents(StagePath);
            try
            {
                Transform puzzle = FindChild(stage.transform, "--- PUZZLE ---");
                if (puzzle == null)
                    throw new InvalidOperationException("OceanStage03 is missing its --- PUZZLE --- container.");

                if (puzzle.Find("Puzzle 3 Turtle Dial Lock") != null)
                {
                    Debug.Log("Puzzle 3 turtle/dial blockout already exists. Its authored layout was preserved.");
                    return;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(blockoutPrefab, puzzle);
                instance.name = "Puzzle 3 Turtle Dial Lock";
                // Kept to the side of the current Stage 3 beacon; designers can freely reposition this root.
                instance.transform.localPosition = new Vector3(9f, 0f, 44f);
                instance.transform.localRotation = Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(stage, StagePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Created the editable Turtle, Pattern Wall, and four Number Dials in OceanStage03.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(stage);
            }
        }

        [MenuItem("Color Puzzle/Rebuild Puzzle 3 Number Dials (Preserve Wall)")]
        public static void RebuildNumberDials()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform oldLock = FindChild(contents.transform, "Four Number Dials Lock");
                Vector3 position = oldLock != null ? oldLock.localPosition : new Vector3(2.5f, 2.5f, 2.35f);
                Quaternion rotation = oldLock != null ? oldLock.localRotation : Quaternion.identity;
                Vector3 scale = oldLock != null ? oldLock.localScale : Vector3.one;
                if (oldLock != null) UnityEngine.Object.DestroyImmediate(oldLock.gameObject);

                CreateNumberDials(contents.transform,
                    RequiredMaterial(GreyStone), RequiredMaterial(Gold), position, rotation, scale);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Rebuilt Puzzle 3's 1–10 ring dials while preserving the wall and all other blockout content.");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static GameObject GetOrCreateBlockoutPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    ConfigureDialLock(contents.transform);
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return existing;
            }

            EnsureFolder(PrefabFolder);
            Material greyStone = RequiredMaterial(GreyStone);
            Material greyArchitecture = RequiredMaterial(GreyArchitecture);
            Material aqua = RequiredMaterial(Aqua);
            Material deepBlue = RequiredMaterial(DeepBlue);
            Material gold = RequiredMaterial(Gold);

            GameObject root = new GameObject("Turtle Dial Lock Blockout");
            try
            {
                CreateTurtle(root.transform, greyStone, aqua, deepBlue);
                CreatePatternWall(root.transform, greyArchitecture, gold);
                CreateNumberDials(root.transform, greyStone, gold,
                    new Vector3(2.5f, 2.5f, 2.35f), Quaternion.identity, Vector3.one);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateTurtle(Transform parent, Material grey, Material aqua, Material shellColor)
        {
            GameObject turtle = new GameObject("Large Turtle");
            turtle.transform.SetParent(parent, false);
            turtle.transform.localPosition = new Vector3(-7f, 1.3f, 0f);

            CreateVisual("Body", PrimitiveType.Sphere, turtle.transform,
                Vector3.zero, new Vector3(4.7f, 1.7f, 3.4f), grey, aqua);
            CreateVisual("Shell", PrimitiveType.Sphere, turtle.transform,
                new Vector3(0f, 0.65f, 0f), new Vector3(4.1f, 1.5f, 2.95f), grey, shellColor);
            CreateVisual("Head", PrimitiveType.Sphere, turtle.transform,
                new Vector3(0f, 0f, -3f), new Vector3(1.5f, 1.15f, 1.35f), grey, aqua);

            Vector3[] flippers =
            {
                new(-4.1f, -0.25f, -0.5f), new(4.1f, -0.25f, -0.5f),
                new(-3.5f, -0.4f, 1.7f), new(3.5f, -0.4f, 1.7f)
            };
            for (int index = 0; index < flippers.Length; index++)
            {
                GameObject flipper = CreateVisual($"Flipper {index + 1}", PrimitiveType.Sphere, turtle.transform,
                    flippers[index], new Vector3(1.8f, 0.25f, 0.8f), grey, aqua);
                flipper.transform.localRotation = Quaternion.Euler(0f, index % 2 == 0 ? -25f : 25f, 0f);
            }
        }

        private static void CreatePatternWall(Transform parent, Material grey, Material color)
        {
            GameObject wall = new GameObject("Pattern Wall");
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = new Vector3(2.5f, 3.2f, 2.8f);

            CreateVisual("Wall Slab", PrimitiveType.Cube, wall.transform,
                Vector3.zero, new Vector3(12.5f, 6.8f, 0.55f), grey, color);

            for (int index = 0; index < 4; index++)
            {
                Material patternColor = RequiredMaterial(PatternMaterials[index]);
                GameObject panel = CreateVisual($"Pattern Display {index + 1}", PrimitiveType.Cube, wall.transform,
                    new Vector3(-4.35f + index * 2.9f, 2.25f, -0.34f),
                    new Vector3(2.25f, 1.15f, 0.1f), grey, patternColor);
                CreatePatternBars(panel.transform, grey, patternColor, index + 2);
            }
        }

        private static void CreatePatternBars(Transform panel, Material grey, Material color, int barCount)
        {
            for (int bar = 0; bar < barCount; bar++)
            {
                CreateVisual($"Pattern Mark {bar + 1}", PrimitiveType.Cube, panel,
                    new Vector3(-0.65f + bar * 0.42f, 0f, -0.7f),
                    new Vector3(0.14f, 0.68f - (bar % 2) * 0.18f, 0.1f), grey, color);
            }
        }

        private static void CreateNumberDials(Transform parent, Material grey, Material color,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            GameObject dialLock = new GameObject("Four Number Dials Lock");
            dialLock.transform.SetParent(parent, false);
            dialLock.transform.localPosition = localPosition;
            dialLock.transform.localRotation = localRotation;
            dialLock.transform.localScale = localScale;

            for (int index = 0; index < 4; index++)
            {
                GameObject dialPrefab = GetOrCreateDialVariant(index, grey, color);
                GameObject dial = (GameObject)PrefabUtility.InstantiatePrefab(dialPrefab, dialLock.transform);
                dial.name = $"Number Dial {index + 1}";
                dial.transform.localPosition = new Vector3(-4.35f + index * 2.9f, 0f, 0f);
            }

            ConfigureDialLock(dialLock.transform.parent);
        }

        private static GameObject GetOrCreateDialVariant(int index, Material grey, Material color)
        {
            string path = string.Format(DialVariantPathFormat, index + 1);
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject basePrefab = GetOrCreateDialBase(grey, color);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = $"Number Dial {index + 1:00}";
                ConfigureDial(instance.GetComponent<NumberDial>(), DialTargets[index],
                    instance.transform.Find("Rotatable Arrow"));
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static GameObject GetOrCreateDialBase(Material grey, Material color)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DialBasePrefabPath);
            if (existing != null) return existing;

            GameObject dial = new GameObject("Number Dial Base");
            try
            {
                NumberDial numberDial = dial.AddComponent<NumberDial>();
                GameObject ring = CreateVisual("Number Ring", PrimitiveType.Cylinder, dial.transform,
                    Vector3.zero, new Vector3(1.02f, 0.16f, 1.02f), grey, color);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                CreateVisual("Dial Backplate", PrimitiveType.Cylinder, dial.transform,
                    new Vector3(0f, 0f, 0.15f), new Vector3(0.82f, 0.09f, 0.82f), grey, color)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                GameObject pointer = new GameObject("Rotatable Arrow");
                pointer.transform.SetParent(dial.transform, false);
                CreateVisual("Arrow Cylinder", PrimitiveType.Cylinder, pointer.transform,
                    new Vector3(0f, 0.36f, -0.25f), new Vector3(0.11f, 0.38f, 0.11f), grey, color);
                CreateNumberRingLabels(dial.transform);
                ConfigureDial(numberDial, DialTargets[0], pointer.transform);
                return PrefabUtility.SaveAsPrefabAsset(dial, DialBasePrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(dial); }
        }

        private static void ConfigureDialLock(Transform root)
        {
            Transform dialLock = FindChild(root, "Four Number Dials Lock");
            if (dialLock == null) return;
            NumberDialLockController controller = dialLock.GetComponent<NumberDialLockController>();
            if (controller == null) controller = dialLock.gameObject.AddComponent<NumberDialLockController>();

            NumberDial[] dials = new NumberDial[DialTargets.Length];
            for (int index = 0; index < DialTargets.Length; index++)
            {
                Transform dialTransform = dialLock.Find($"Number Dial {index + 1}");
                if (dialTransform == null) continue;
                NumberDial dial = dialTransform.GetComponent<NumberDial>();
                if (dial == null) dial = dialTransform.gameObject.AddComponent<NumberDial>();
                dials[index] = dial;
            }

            SerializedObject controllerData = new SerializedObject(controller);
            SerializedProperty dialList = controllerData.FindProperty("dials");
            dialList.arraySize = dials.Length;
            for (int index = 0; index < dials.Length; index++)
                dialList.GetArrayElementAtIndex(index).objectReferenceValue = dials[index];
            controllerData.FindProperty("wrongCombinationClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(WrongCombinationClip);
            controllerData.FindProperty("wrongCombinationVolume").floatValue = 0.42f;
            controllerData.FindProperty("correctCombinationClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(CorrectCombinationClip);
            controllerData.FindProperty("correctCombinationVolume").floatValue = 0.3f;
            controllerData.FindProperty("revealPuzzleColorOnSuccess").boolValue = true;
            controllerData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDial(NumberDial dial, int target, Transform pointer)
        {
            SerializedObject data = new SerializedObject(dial);
            data.FindProperty("targetValue").intValue = target;
            data.FindProperty("currentValue").intValue = 1;
            data.FindProperty("pointer").objectReferenceValue = pointer;
            data.FindProperty("valueText").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateNumberRingLabels(Transform parent)
        {
            const float radius = 0.73f;
            for (int number = 1; number <= 10; number++)
            {
                float angle = Mathf.Deg2Rad * (90f - (number - 1) * 36f);
                GameObject labelObject = new GameObject($"Number {number}");
                labelObject.transform.SetParent(parent, false);
                labelObject.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.24f);
                labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                TextMesh label = labelObject.AddComponent<TextMesh>();
                label.text = number.ToString();
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = 0.18f;
                label.fontSize = 64;
                label.color = Color.white;
            }
        }

        private static GameObject CreateVisual(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material grey, Material color)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = grey;
            DualViewMaterial dual = visual.AddComponent<DualViewMaterial>();
            SerializedObject data = new SerializedObject(dual);
            SetMaterial(data.FindProperty("greyscaleMaterials"), grey);
            SetMaterial(data.FindProperty("colorMaterials"), color);
            data.ApplyModifiedPropertiesWithoutUndo();
            return visual;
        }

        private static void SetMaterial(SerializedProperty property, Material material)
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = material;
        }

        private static Material RequiredMaterial(string path) =>
            AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new InvalidOperationException($"Missing material: {path}");

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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
#endif
