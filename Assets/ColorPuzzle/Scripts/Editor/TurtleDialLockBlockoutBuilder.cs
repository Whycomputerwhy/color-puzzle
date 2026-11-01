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

        private const string GreyStone = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Stone.mat";
        private const string GreyArchitecture = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Architecture.mat";
        private const string Aqua = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_Aqua.mat";
        private const string DeepBlue = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_DeepBlue.mat";
        private const string Gold = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_TreasureGold.mat";
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
                CreateNumberDials(root.transform, greyStone, gold);
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

        private static void CreateNumberDials(Transform parent, Material grey, Material color)
        {
            GameObject dialLock = new GameObject("Four Number Dials Lock");
            dialLock.transform.SetParent(parent, false);
            dialLock.transform.localPosition = new Vector3(2.5f, 2.5f, 2.35f);

            for (int index = 0; index < 4; index++)
            {
                GameObject dial = new GameObject($"Number Dial {index + 1}");
                dial.transform.SetParent(dialLock.transform, false);
                dial.transform.localPosition = new Vector3(-4.35f + index * 2.9f, 0f, 0f);
                NumberDial numberDial = dial.AddComponent<NumberDial>();

                GameObject wheel = CreateVisual("Rotatable Wheel", PrimitiveType.Cylinder, dial.transform,
                    Vector3.zero, new Vector3(0.92f, 0.18f, 0.92f), grey, color);
                wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                GameObject pointer = CreateVisual("Number Pointer", PrimitiveType.Cube, dial.transform,
                    new Vector3(0f, 0.36f, -0.23f), new Vector3(0.14f, 0.48f, 0.1f), grey, color);
                CreateVisual("Dial Base", PrimitiveType.Cube, dial.transform,
                    new Vector3(0f, 0f, 0.18f), new Vector3(1.35f, 1.35f, 0.22f), grey, color);
                TextMesh label = CreateValueLabel(dial.transform);
                ConfigureDial(numberDial, DialTargets[index], pointer.transform, label);
            }

            ConfigureDialLock(dialLock.transform.parent);
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
                Transform pointer = dialTransform.Find("Number Pointer");
                TextMesh label = dialTransform.GetComponentInChildren<TextMesh>(true);
                if (label == null) label = CreateValueLabel(dialTransform);
                ConfigureDial(dial, DialTargets[index], pointer, label);
                dials[index] = dial;
            }

            SerializedObject controllerData = new SerializedObject(controller);
            SerializedProperty dialList = controllerData.FindProperty("dials");
            dialList.arraySize = dials.Length;
            for (int index = 0; index < dials.Length; index++)
                dialList.GetArrayElementAtIndex(index).objectReferenceValue = dials[index];
            controllerData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDial(NumberDial dial, int target, Transform pointer, TextMesh label)
        {
            SerializedObject data = new SerializedObject(dial);
            data.FindProperty("targetValue").intValue = target;
            data.FindProperty("currentValue").intValue = 0;
            data.FindProperty("pointer").objectReferenceValue = pointer;
            data.FindProperty("valueText").objectReferenceValue = label;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextMesh CreateValueLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("Number Value");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, -0.05f, -0.34f);
            labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = "0";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.42f;
            label.fontSize = 64;
            label.color = Color.white;
            return label;
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
