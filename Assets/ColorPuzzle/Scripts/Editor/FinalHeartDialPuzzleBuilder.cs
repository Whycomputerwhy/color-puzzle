#if UNITY_EDITOR
using System;
using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    /// <summary>Builds Puzzle 4 from the existing dial/key prefabs without touching other stages.</summary>
    public static class FinalHeartDialPuzzleBuilder
    {
        // CENTRALIZED PUZZLE 4 AUTHORING PARAMETERS
        private const string StagePath = "Assets/ColorPuzzle/Prefabs/Level/CampaignStages/OceanStage04.prefab";
        private const string DialPrefabFormat = "Assets/ColorPuzzle/Prefabs/Gameplay/TurtleDialLock/Number Dial {0:00}.prefab";
        private const string KeyPrefabFormat = "Assets/ColorPuzzle/Prefabs/Gameplay/FishPuzzle/Pattern Stone {0:00} {1}.prefab";
        private const string CorrectClip = "Assets/ColorPuzzle/Resources/Audio/CorrectShellPlacementAndReveal.wav";
        private const string WrongClip = "Assets/ColorPuzzle/Resources/Audio/WrongShellPlacement.wav";
        private const string GlassMaterial = "Assets/ColorPuzzle/Materials/ShellPuzzle/Clam Casing Transparent.mat";
        private const string GreyStone = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Stone.mat";
        private static readonly string[] ColorMaterials =
        {
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleBlue.mat",
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleYellow.mat",
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleRed.mat",
            "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_PuzzleGreen.mat"
        };
        private static readonly string[] Shapes = { "Triangle", "Pentagon", "Square", "Line" };
        private static readonly int[] Answers = { 8, 4, 5, 10 };
        private static readonly Vector3 PuzzleAnchor = new(0f, 0f, 15f);
        private const float DialSpacing = 4.5f;
        private const float DialScale = 1.45f;
        private const float GlassRadius = 4.2f;
        private const float GlassHeight = 7.8f;
        private const float HeartHeight = 4.5f;

        [MenuItem("Color Puzzle/Build Puzzle 4 Heart Dial Blockout")]
        public static void Build()
        {
            GameObject stage = PrefabUtility.LoadPrefabContents(StagePath);
            try
            {
                Transform anchor = FindChild(stage.transform, "--- PUZZLE ---")
                    ?? throw new InvalidOperationException("OceanStage04 is missing its --- PUZZLE --- container.");
                if (anchor.Find("Final Heart Dial Puzzle") != null)
                {
                    ApplyDialColors(anchor.Find("Final Heart Dial Puzzle"));
                    PrefabUtility.SaveAsPrefabAsset(stage, StagePath);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Puzzle 4 already exists; applied its palette without changing authored layout.");
                    return;
                }

                GameObject root = new GameObject("Final Heart Dial Puzzle");
                root.transform.SetParent(anchor, false);
                root.transform.localPosition = PuzzleAnchor;

                GameObject glass = CreateGlassCasing(root.transform);
                Renderer heart = CreateHeart(root.transform);
                FinalHeartVictory victory = root.AddComponent<FinalHeartVictory>();
                SetPrivate(victory, "heartRenderer", heart);
                SetPrivate(victory, "glassCasing", glass);

                GameObject dialRoot = new GameObject("Four Combination Dials");
                dialRoot.transform.SetParent(root.transform, false);
                GameObject keyRoot = new GameObject("Shape Key Stones");
                keyRoot.transform.SetParent(root.transform, false);
                NumberDial[] dials = new NumberDial[4];
                FinalDialKeySlot[] slots = new FinalDialKeySlot[4];

                for (int index = 0; index < 4; index++)
                {
                    float x = (index - 1.5f) * DialSpacing;
                    GameObject dial = InstantiatePrefab(string.Format(DialPrefabFormat, index + 1), dialRoot.transform);
                    dial.name = $"Combination Dial {index + 1} — {Shapes[index]} {Answers[index]}";
                    dial.transform.localPosition = new Vector3(x, 0.08f, 0f);
                    dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    dial.transform.localScale = Vector3.one * DialScale;
                    ApplyDialColor(dial, Required(ColorMaterials[index]));
                    dials[index] = dial.GetComponent<NumberDial>();

                    slots[index] = CreateSlot(dial.transform, index);

                    GameObject key = InstantiatePrefab(string.Format(KeyPrefabFormat, index + 1, Shapes[index]), keyRoot.transform);
                    key.name = $"Shape Key Stone {index + 1} — {Shapes[index]}";
                    key.AddComponent<FinalDialKeyColorLock>();
                    float side = index % 2 == 0 ? -1f : 1f;
                    key.transform.localPosition = new Vector3(x + side * 2.1f, 0.06f, 4.0f + (index / 2) * 2.4f);
                    key.transform.localRotation = Quaternion.Euler(0f, index * 24f, 0f);
                }

                NumberDialLockController controller = dialRoot.AddComponent<NumberDialLockController>();
                SerializedObject data = new SerializedObject(controller);
                AssignArray(data.FindProperty("dials"), dials);
                AssignArray(data.FindProperty("keySlots"), slots);
                data.FindProperty("campaignStageIndex").intValue = 3;
                data.FindProperty("wrongCombinationClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(WrongClip);
                data.FindProperty("correctCombinationClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(CorrectClip);
                data.FindProperty("finalHeartVictory").objectReferenceValue = victory;
                data.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(stage, StagePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Built Puzzle 4: heart casing, four combined dials, and four reusable shape-key stones.");
            }
            finally { PrefabUtility.UnloadPrefabContents(stage); }
        }

        private static GameObject CreateGlassCasing(Transform parent)
        {
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glass.name = "Glass Heart Casing";
            glass.transform.SetParent(parent, false);
            glass.transform.localPosition = new Vector3(0f, GlassHeight * 0.5f, 0f);
            glass.transform.localScale = new Vector3(GlassRadius * 2f, GlassHeight * 0.5f, GlassRadius * 2f);
            UnityEngine.Object.DestroyImmediate(glass.GetComponent<Collider>());
            SetDual(glass.GetComponent<Renderer>(), Required(GlassMaterial), Required(GlassMaterial));
            return glass;
        }

        private static Renderer CreateHeart(Transform parent)
        {
            GameObject heart = new GameObject("Heart");
            heart.transform.SetParent(parent, false);
            heart.transform.localPosition = new Vector3(0f, HeartHeight, 0f);
            Material grey = Required(GreyStone);
            Material color = Required(ColorMaterials[0]);
            Renderer reference = null;
            for (int index = 0; index < 2; index++)
            {
                GameObject lobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lobe.name = index == 0 ? "Heart Lobe Left" : "Heart Lobe Right";
                lobe.transform.SetParent(heart.transform, false);
                lobe.transform.localPosition = new Vector3(index == 0 ? -0.55f : 0.55f, 0.25f, 0f);
                lobe.transform.localScale = Vector3.one * 1.15f;
                UnityEngine.Object.DestroyImmediate(lobe.GetComponent<Collider>());
                Renderer renderer = lobe.GetComponent<Renderer>();
                SetDual(renderer, grey, color);
                if (reference == null) reference = renderer;
            }
            GameObject point = GameObject.CreatePrimitive(PrimitiveType.Cube);
            point.name = "Heart Point";
            point.transform.SetParent(heart.transform, false);
            point.transform.localPosition = new Vector3(0f, -0.75f, 0f);
            point.transform.localRotation = Quaternion.Euler(0f, 45f, 45f);
            point.transform.localScale = new Vector3(1.3f, 1.3f, 0.7f);
            UnityEngine.Object.DestroyImmediate(point.GetComponent<Collider>());
            SetDual(point.GetComponent<Renderer>(), grey, color);
            return reference;
        }

        private static FinalDialKeySlot CreateSlot(Transform dial, int index)
        {
            GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            slot.name = $"Shape Key Slot — {Shapes[index]}";
            slot.transform.SetParent(dial, false);
            slot.transform.localPosition = new Vector3(0f, 0f, -0.31f);
            slot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            slot.transform.localScale = new Vector3(0.38f, 0.06f, 0.38f);
            Renderer renderer = slot.GetComponent<Renderer>();
            SetDual(renderer, Required(GreyStone), Required(ColorMaterials[index]));
            FinalDialKeySlot keySlot = slot.AddComponent<FinalDialKeySlot>();
            GameObject anchor = new GameObject("Key Anchor");
            anchor.transform.SetParent(slot.transform, false);
            anchor.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            SerializedObject data = new SerializedObject(keySlot);
            data.FindProperty("requiredKeyNumber").intValue = index + 1;
            data.FindProperty("keyAnchor").objectReferenceValue = anchor.transform;
            data.FindProperty("recessRenderer").objectReferenceValue = renderer;
            data.ApplyModifiedPropertiesWithoutUndo();
            return keySlot;
        }

        private static void SetDual(Renderer renderer, Material grey, Material color)
        {
            renderer.sharedMaterial = grey;
            DualViewMaterial visual = renderer.gameObject.AddComponent<DualViewMaterial>();
            SerializedObject data = new SerializedObject(visual);
            SetMaterial(data.FindProperty("greyscaleMaterials"), grey);
            SetMaterial(data.FindProperty("colorMaterials"), color);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyDialColors(Transform root)
        {
            Transform dials = root.Find("Four Combination Dials");
            if (dials != null)
            for (int index = 0; index < Mathf.Min(4, dials.childCount); index++)
            {
                Transform dial = dials.GetChild(index);
                dial.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ApplyDialColor(dial.gameObject, Required(ColorMaterials[index]));
                foreach (FinalDialKeySlot slot in dial.GetComponentsInChildren<FinalDialKeySlot>(true))
                {
                    Transform anchor = slot.transform.Find("Key Anchor");
                    if (anchor != null) anchor.localPosition = new Vector3(0f, -0.08f, 0f);
                }
            }

            Transform keys = root.Find("Shape Key Stones");
            if (keys == null) return;
            foreach (ShellPuzzlePiece key in keys.GetComponentsInChildren<ShellPuzzlePiece>(true))
                if (key.GetComponent<FinalDialKeyColorLock>() == null)
                    key.gameObject.AddComponent<FinalDialKeyColorLock>();
        }

        private static void ApplyDialColor(GameObject dial, Material color)
        {
            foreach (DualViewMaterial visual in dial.GetComponentsInChildren<DualViewMaterial>(true))
            {
                SerializedObject data = new SerializedObject(visual);
                SetMaterial(data.FindProperty("colorMaterials"), color);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetPrivate(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static GameObject InstantiatePrefab(string path, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException($"Missing prefab: {path}");
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        private static Material Required(string path) =>
            AssetDatabase.LoadAssetAtPath<Material>(path)
            ?? throw new InvalidOperationException($"Missing material: {path}");

        private static void SetMaterial(SerializedProperty property, Material material)
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = material;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform match = FindChild(child, name);
                if (match != null) return match;
            }
            return null;
        }
    }
}
#endif
