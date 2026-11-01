#if UNITY_EDITOR
using System;
using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    /// <summary>
    /// Owns Puzzle 2's round keyhole pillars. The one-time replacement command only
    /// swaps visuals, retaining every authored transform beneath the Keyholes group.
    /// </summary>
    public static class RoundPuzzleTwoKeyholeBuilder
    {
        private const string PrefabFolder = "Assets/ColorPuzzle/Prefabs/Gameplay/FishPuzzle";
        private const string StagePath = "Assets/ColorPuzzle/Prefabs/Level/CampaignStages/OceanStage02.prefab";
        private static readonly float[] Diameters = { 0.42f, 0.56f, 0.70f, 0.84f };
        private static readonly Color[] Palette =
        {
            new(0.25f, 0.77f, 0.96f), new(0.97f, 0.79f, 0.28f),
            new(0.94f, 0.28f, 0.28f), new(0.22f, 0.78f, 0.36f)
        };

        [MenuItem("Color Puzzle/Create Puzzle 2 Round Keyhole Pillars")]
        public static void CreatePrefabs()
        {
            EnsureFolder(PrefabFolder);
            for (int index = 0; index < 4; index++)
                GetOrCreateVariant(index);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created the four reusable round Puzzle 2 keyhole pillar prefabs. No stage layout was changed.");
        }

        [MenuItem("Color Puzzle/Use Round Keyhole Pillars in Puzzle 2 (Preserve Layout)")]
        public static void ReplacePuzzleTwoKeyholes()
        {
            CreatePrefabs();
            GameObject stage = PrefabUtility.LoadPrefabContents(StagePath);
            try
            {
                GameObject keyholesObject = FindChild(stage.transform, "Keyholes");
                if (keyholesObject == null)
                    throw new InvalidOperationException("Puzzle 2 has no Keyholes group to replace.");
                Transform keyholes = keyholesObject.transform;

                for (int index = 0; index < 4; index++)
                {
                    Transform old = keyholes.Find($"Keyhole {index + 1}");
                    if (old == null) continue;

                    GameObject replacementPrefab = LoadVariant(index);
                    GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(replacementPrefab, keyholes);
                    replacement.name = old.name;
                    replacement.transform.SetSiblingIndex(old.GetSiblingIndex());
                    replacement.transform.localPosition = old.localPosition;
                    replacement.transform.localRotation = old.localRotation;
                    replacement.transform.localScale = old.localScale;
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                }

                PrefabUtility.SaveAsPrefabAsset(stage, StagePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Puzzle 2 now uses round keyhole pillars. Their authored positions, rotations, and scales were preserved.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(stage);
            }
        }

        private static GameObject LoadVariant(int index) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Round Keyhole Pillar {index + 1:00}.prefab");

        private static void GetOrCreateVariant(int index)
        {
            string path = $"{PrefabFolder}/Round Keyhole Pillar {index + 1:00}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            GameObject root = new GameObject($"Round Keyhole Pillar {index + 1:00}");
            try
            {
                ShellPuzzlePiece piece = root.AddComponent<ShellPuzzlePiece>();
                SetIdentity(piece, index + 1, Diameters[index], Palette[index]);
                Material stoneGrey = MaterialAt("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Stone.mat");
                Material stoneColor = MaterialAt("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_Sand.mat");
                Material recessGrey = MaterialAt("Assets/ColorPuzzle/Materials/ShellPuzzle/Keyhole Recess Grey.mat");
                Material recessColor = MaterialAt("Assets/ColorPuzzle/Materials/ShellPuzzle/Keyhole Recess.mat");
                Material rimGrey = MaterialAt("Assets/ColorPuzzle/Materials/ShellPuzzle/Keyhole Rim Grey.mat");
                Material rimColor = MaterialAt("Assets/ColorPuzzle/Materials/ShellPuzzle/Keyhole Rim.mat");

                Primitive("Stone Pillar", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, 0.75f, 0f), new Vector3(1.3f, 0.75f, 1.3f), stoneGrey, stoneColor);
                Primitive("Slot Rim", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, 1.505f, 0f), new Vector3(Diameters[index] + 0.16f, 0.01f, Diameters[index] + 0.16f), rimGrey, rimColor);
                Primitive("Slot Recess", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, 1.517f, 0f), new Vector3(Diameters[index], 0.01f, Diameters[index]), recessGrey, recessColor);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Primitive(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material grey, Material color)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.GetComponent<Renderer>().sharedMaterial = grey;
            DualViewMaterial dual = visual.AddComponent<DualViewMaterial>();
            SerializedObject serialized = new SerializedObject(dual);
            SetMaterial(serialized.FindProperty("greyscaleMaterials"), grey);
            SetMaterial(serialized.FindProperty("colorMaterials"), color);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIdentity(ShellPuzzlePiece piece, int pair, float fitDiameter, Color pearlColor)
        {
            SerializedObject serialized = new SerializedObject(piece);
            serialized.FindProperty("kind").enumValueIndex = (int)ShellPuzzlePiece.PieceKind.Keyhole;
            serialized.FindProperty("pairNumber").intValue = pair;
            serialized.FindProperty("fitDiameter").floatValue = fitDiameter;
            serialized.FindProperty("pearlColor").colorValue = pearlColor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMaterial(SerializedProperty array, Material material)
        {
            array.arraySize = 1;
            array.GetArrayElementAtIndex(0).objectReferenceValue = material;
        }

        private static Material MaterialAt(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException($"Missing material: {path}");
            return material;
        }

        private static GameObject FindChild(Transform root, string name)
        {
            if (root.name == name) return root.gameObject;
            foreach (Transform child in root)
            {
                GameObject found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
