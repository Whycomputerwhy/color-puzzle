#if UNITY_EDITOR
using System;
using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    /// <summary>Creates the authored fish-school and shell-pair blockout for campaign stage 2.</summary>
    public static class FishPuzzleStageBuilder
    {
        private const string Stage2Path = "Assets/ColorPuzzle/Prefabs/Level/CampaignStages/OceanStage02.prefab";
        private const string Stage1Path = "Assets/ColorPuzzle/Prefabs/Level/CampaignStages/OceanStage01.prefab";
        private const string ShellPrefabFolder = "Assets/ColorPuzzle/Prefabs/Gameplay/ShellPuzzle";
        private const string FishPrefabFolder = "Assets/ColorPuzzle/Prefabs/Gameplay/Fish";
        private const string PatternStonePrefabFolder = "Assets/ColorPuzzle/Prefabs/Gameplay/FishPuzzle";
        private const string FishPrefabPath = FishPrefabFolder + "/Underwater Fish.prefab";
        private const string GreyFishMaterial = "Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Shell.mat";
        private static readonly string[] PatternNames = { "Triangle", "Pentagon", "Square", "Line" };
        private static readonly float[] PatternKeyDiameters = { 0.42f, 0.56f, 0.7f, 0.84f };
        // Locked campaign order: smallest pearl/key to largest is blue, yellow, red, green.
        // Puzzle 2 keeps that exact order for triangle, pentagon, square, and line.
        private static readonly string[] PaletteNames = { "Blue", "Yellow", "Red", "Green" };
        private static readonly Color[] PaletteColors =
        {
            new(0.25f, 0.77f, 0.96f), new(0.97f, 0.79f, 0.28f),
            new(0.94f, 0.28f, 0.28f), new(0.22f, 0.78f, 0.36f)
        };

        [MenuItem("Color Puzzle/Build Puzzle 2 Fish Blockout")]
        public static void Build()
        {
            GameObject stage = PrefabUtility.LoadPrefabContents(Stage2Path);
            GameObject stage1 = PrefabUtility.LoadPrefabContents(Stage1Path);
            try
            {
                ApplySharedPuzzlePalette();
                Transform puzzleAnchor = FindChild(stage.transform, "--- PUZZLE ---");
                if (puzzleAnchor == null)
                    throw new InvalidOperationException("OceanStage02 is missing its --- PUZZLE --- container.");

                Transform oldBlockout = puzzleAnchor.Find("Fish Puzzle Blockout");
                if (oldBlockout != null)
                {
                    // This level has been manually laid out. Never replace an existing
                    // blockout from this utility; use the palette-only command below.
                    AssetDatabase.SaveAssets();
                    Debug.Log("Puzzle 2 already exists. Its authored layout was preserved.");
                    return;
                }

                StageGateBeacon oldBeacon = stage.GetComponentInChildren<StageGateBeacon>(true);
                if (oldBeacon != null) UnityEngine.Object.DestroyImmediate(oldBeacon.gameObject);

                ShellPuzzleController shellPuzzle = stage.GetComponent<ShellPuzzleController>();
                if (shellPuzzle == null) shellPuzzle = stage.AddComponent<ShellPuzzleController>();
                ShellPuzzleController stageOnePuzzle = stage1.GetComponent<ShellPuzzleController>();
                if (stageOnePuzzle != null) EditorUtility.CopySerialized(stageOnePuzzle, shellPuzzle);

                GameObject blockout = new GameObject("Fish Puzzle Blockout");
                blockout.transform.SetParent(puzzleAnchor, false);

                CreateShellCombination(blockout.transform);
                CreateFishSchool(blockout.transform, GetOrCreateFishPrefab());

                PrefabUtility.SaveAsPrefabAsset(stage, Stage2Path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Puzzle 2 now has fourteen roaming fish and a centered four-pair shell puzzle.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(stage1);
                PrefabUtility.UnloadPrefabContents(stage);
            }
        }

        [MenuItem("Color Puzzle/Apply Locked Puzzle Colors (No Layout Changes)")]
        public static void ApplyLockedPuzzleColors()
        {
            ApplySharedPuzzlePalette();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied the locked blue, yellow, red, green palette without changing any layout.");
        }

        private static void CreateShellCombination(Transform parent)
        {
            GameObject combination = new GameObject("Centered Shell Combination");
            combination.transform.SetParent(parent, false);
            combination.transform.localPosition = new Vector3(0f, 0f, 15f);

            GameObject keyholes = new GameObject("Keyholes");
            keyholes.transform.SetParent(combination.transform, false);
            GameObject keys = new GameObject("Shell Keys");
            keys.transform.SetParent(combination.transform, false);

            Vector3[] keyholePositions =
            {
                new(-5.4f, 0f, 0f), new(-1.8f, 0f, 0f), new(1.8f, 0f, 0f), new(5.4f, 0f, 0f)
            };
            Vector3[] keyPositions =
            {
                new(-5.4f, 0.08f, 4f), new(-1.8f, 0.08f, 4f), new(1.8f, 0.08f, 4f), new(5.4f, 0.08f, 4f)
            };

            for (int index = 0; index < 4; index++)
            {
                GameObject keyhole = LoadPrefab($"Shell Keyhole {index + 1:00}");
                GameObject key = GetOrCreatePatternStonePrefab(index);
                GameObject keyholeInstance = (GameObject)PrefabUtility.InstantiatePrefab(keyhole, keyholes.transform);
                keyholeInstance.name = $"Keyhole {index + 1}";
                keyholeInstance.transform.localPosition = keyholePositions[index];
                GameObject keyInstance = (GameObject)PrefabUtility.InstantiatePrefab(key, keys.transform);
                keyInstance.name = $"Pattern Stone {index + 1:00} — {PatternNames[index]}";
                keyInstance.transform.localPosition = keyPositions[index];
                keyInstance.transform.localRotation = Quaternion.Euler(0f, index * 12f, 0f);
            }
        }

        private static GameObject GetOrCreatePatternStonePrefab(int index)
        {
            EnsureFolder(PatternStonePrefabFolder);
            string path = $"{PatternStonePrefabFolder}/Pattern Stone {index + 1:00} {PatternNames[index]}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                // Keep previously authored stone geometry, but bring its coloured pattern
                // into the shared campaign palette whenever this builder is re-run.
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (DualViewMaterial visual in contents.GetComponentsInChildren<DualViewMaterial>(true))
                    {
                        if (!visual.name.StartsWith("Pattern Segment")) continue;
                        SetColorMaterial(visual, GetOrCreatePaletteMaterial(index));
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return existing;
            }

            Material stoneGrey = RequiredMaterial("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Stone.mat");
            Material stoneColor = RequiredMaterial("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_Sand.mat");
            Material patternGrey = RequiredMaterial("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Architecture.mat");
            Material patternColor = GetOrCreatePaletteMaterial(index);
            GameObject root = new GameObject($"Pattern Stone {index + 1:00} {PatternNames[index]}");
            ShellPuzzlePiece piece = root.AddComponent<ShellPuzzlePiece>();
            SetPatternStoneIdentity(piece, index + 1, PatternKeyDiameters[index]);

            CreateStoneVisual(root.transform, stoneGrey, stoneColor);
            CreatePattern(root.transform, PatternNames[index], patternGrey, patternColor);
            try { return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CreateStoneVisual(Transform parent, Material grey, Material color)
        {
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Beige Cube"; // Existing interaction/highlight code uses this stable visual name.
            stone.transform.SetParent(parent, false);
            stone.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            stone.transform.localScale = new Vector3(0.76f, 0.09f, 0.6f);
            SetDualViewAppearance(stone.GetComponent<Renderer>(), grey, color);
        }

        private static void CreatePattern(Transform parent, string pattern, Material grey, Material color)
        {
            Vector2[] points = pattern switch
            {
                "Triangle" => PolygonPoints(3, 0.24f, Mathf.PI * 0.5f),
                "Pentagon" => PolygonPoints(5, 0.25f, Mathf.PI * 0.5f),
                "Square" => PolygonPoints(4, 0.23f, Mathf.PI * 0.25f),
                _ => new[] { new Vector2(-0.25f, 0f), new Vector2(0.25f, 0f) }
            };

            for (int index = 0; index < points.Length - (pattern == "Line" ? 1 : 0); index++)
            {
                Vector2 start = points[index];
                Vector2 end = points[(index + 1) % points.Length];
                CreatePatternSegment(parent, start, end, grey, color, index + 1);
            }
        }

        private static Vector2[] PolygonPoints(int sides, float radius, float startRadians)
        {
            Vector2[] points = new Vector2[sides];
            for (int index = 0; index < sides; index++)
            {
                float angle = startRadians + (Mathf.PI * 2f * index / sides);
                points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static void CreatePatternSegment(Transform parent, Vector2 start, Vector2 end,
            Material grey, Material color, int index)
        {
            Vector2 direction = end - start;
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = $"Pattern Segment {index}";
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = new Vector3(
                (start.x + end.x) * 0.5f, 0.165f, (start.y + end.y) * 0.5f);
            segment.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 0f);
            segment.transform.localScale = new Vector3(direction.magnitude, 0.025f, 0.045f);
            UnityEngine.Object.DestroyImmediate(segment.GetComponent<Collider>());
            SetDualViewAppearance(segment.GetComponent<Renderer>(), grey, color);
        }

        private static void SetPatternStoneIdentity(ShellPuzzlePiece piece, int pair, float fitDiameter)
        {
            SerializedObject data = new SerializedObject(piece);
            data.FindProperty("kind").enumValueIndex = (int)ShellPuzzlePiece.PieceKind.Key;
            data.FindProperty("pairNumber").intValue = pair;
            data.FindProperty("fitDiameter").floatValue = fitDiameter;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetDualViewAppearance(Renderer renderer, Material grey, Material color)
        {
            renderer.sharedMaterial = grey;
            DualViewMaterial dualView = renderer.gameObject.AddComponent<DualViewMaterial>();
            SerializedObject data = new SerializedObject(dualView);
            SetMaterial(data.FindProperty("greyscaleMaterials"), grey);
            SetMaterial(data.FindProperty("colorMaterials"), color);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateFishSchool(Transform parent, FishSwimmer fishPrefab)
        {
            GameObject school = new GameObject("Fish School");
            school.transform.SetParent(parent, false);
            school.transform.localPosition = new Vector3(0f, 0f, 15f);
            FishSchoolController controller = school.AddComponent<FishSchoolController>();
            FishSwimmer blue = GetOrCreateFishVariant("Fish Blue", fishPrefab, GetOrCreatePaletteMaterial(0));
            FishSwimmer yellow = GetOrCreateFishVariant("Fish Yellow", fishPrefab, GetOrCreatePaletteMaterial(1));
            FishSwimmer red = GetOrCreateFishVariant("Fish Red", fishPrefab, GetOrCreatePaletteMaterial(2));
            FishSwimmer green = GetOrCreateFishVariant("Fish Green", fishPrefab, GetOrCreatePaletteMaterial(3));
            SerializedObject data = new SerializedObject(controller);
            SerializedProperty groups = data.FindProperty("fishGroups");
            groups.arraySize = 4;
            SetFishGroup(groups.GetArrayElementAtIndex(0), "Blue", blue, 3);
            SetFishGroup(groups.GetArrayElementAtIndex(1), "Yellow", yellow, 5);
            SetFishGroup(groups.GetArrayElementAtIndex(2), "Red", red, 4);
            SetFishGroup(groups.GetArrayElementAtIndex(3), "Green", green, 2);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static FishSwimmer GetOrCreateFishPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FishPrefabPath);
            if (existing != null) return existing.GetComponent<FishSwimmer>();

            EnsureFolder(FishPrefabFolder);
            GameObject fish = new GameObject("Underwater Fish");
            FishSwimmer swimmer = fish.AddComponent<FishSwimmer>();
            Material grey = RequiredMaterial(GreyFishMaterial);
            Material color = RequiredMaterial("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_Aqua.mat");

            CreateFishVisual("Body", PrimitiveType.Sphere, fish.transform,
                new Vector3(0f, 0f, 0.04f), new Vector3(0.42f, 0.34f, 0.85f), grey, color);
            CreateFishVisual("Tail", PrimitiveType.Cube, fish.transform,
                new Vector3(0f, 0f, -0.48f), new Vector3(0.06f, 0.48f, 0.42f), grey, color);
            CreateFishVisual("Top Fin", PrimitiveType.Cube, fish.transform,
                new Vector3(0f, 0.27f, -0.02f), new Vector3(0.08f, 0.16f, 0.25f), grey, color);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(fish, FishPrefabPath);
                return prefab.GetComponent<FishSwimmer>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fish);
            }
        }

        private static FishSwimmer GetOrCreateFishVariant(string name, FishSwimmer baseFish, Material color)
        {
            string path = $"{FishPrefabFolder}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // Load editable prefab contents in both cases. Instantiating the persistent
            // base prefab directly here creates loose editor objects instead of a variant.
            string sourcePath = existing != null ? path : AssetDatabase.GetAssetPath(baseFish.gameObject);
            GameObject instance = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                instance.name = name;
                foreach (DualViewMaterial visual in instance.GetComponentsInChildren<DualViewMaterial>(true))
                {
                    SerializedObject data = new SerializedObject(visual);
                    SetMaterial(data.FindProperty("colorMaterials"), color);
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
                return variant.GetComponent<FishSwimmer>();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static Material GetOrCreatePaletteMaterial(int index)
        {
            string path = $"Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_Puzzle{PaletteNames[index]}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.HasProperty("_Color")) existing.SetColor("_Color", PaletteColors[index]);
                if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", PaletteColors[index]);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Material source = RequiredMaterial("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Env_CoralPink.mat");
            Material paletteMaterial = new Material(source) { name = $"M_Cel_Env_Puzzle{PaletteNames[index]}" };
            if (paletteMaterial.HasProperty("_Color")) paletteMaterial.SetColor("_Color", PaletteColors[index]);
            if (paletteMaterial.HasProperty("_BaseColor")) paletteMaterial.SetColor("_BaseColor", PaletteColors[index]);
            AssetDatabase.CreateAsset(paletteMaterial, path);
            return paletteMaterial;
        }

        private static void ApplySharedPuzzlePalette()
        {
            for (int index = 0; index < PaletteColors.Length; index++)
            {
                Material pearl = RequiredMaterial($"Assets/ColorPuzzle/Materials/ShellPuzzle/Pearl {index + 1:00}.mat");
                if (pearl.HasProperty("_Color")) pearl.SetColor("_Color", PaletteColors[index]);
                if (pearl.HasProperty("_BaseColor")) pearl.SetColor("_BaseColor", PaletteColors[index]);
                EditorUtility.SetDirty(pearl);
                UpdateShellPiecePalette($"{ShellPrefabFolder}/Shell Key {index + 1:00}.prefab", PaletteColors[index]);
                UpdateShellPiecePalette($"{ShellPrefabFolder}/Shell Keyhole {index + 1:00}.prefab", PaletteColors[index]);
            }

            GameObject stageOne = PrefabUtility.LoadPrefabContents(Stage1Path);
            try
            {
                foreach (ShellPuzzlePiece piece in stageOne.GetComponentsInChildren<ShellPuzzlePiece>(true))
                    SetShellPieceColor(piece, PaletteColors[piece.PairNumber - 1]);
                PrefabUtility.SaveAsPrefabAsset(stageOne, Stage1Path);
            }
            finally { PrefabUtility.UnloadPrefabContents(stageOne); }
        }

        private static void UpdateShellPiecePalette(string path, Color color)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SetShellPieceColor(contents.GetComponent<ShellPuzzlePiece>(), color);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void SetShellPieceColor(ShellPuzzlePiece piece, Color color)
        {
            if (piece == null) return;
            SerializedObject data = new SerializedObject(piece);
            data.FindProperty("pearlColor").colorValue = color;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColorMaterial(DualViewMaterial visual, Material color)
        {
            SerializedObject data = new SerializedObject(visual);
            SetMaterial(data.FindProperty("colorMaterials"), color);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFishGroup(SerializedProperty group, string label, FishSwimmer prefab, int count)
        {
            group.FindPropertyRelative("label").stringValue = label;
            group.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            group.FindPropertyRelative("count").intValue = count;
        }

        private static void CreateFishVisual(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material grey, Material color)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = grey;
            DualViewMaterial dualView = visual.AddComponent<DualViewMaterial>();
            SerializedObject data = new SerializedObject(dualView);
            SetMaterial(data.FindProperty("greyscaleMaterials"), grey);
            SetMaterial(data.FindProperty("colorMaterials"), color);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMaterial(SerializedProperty property, Material material)
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = material;
        }

        private static GameObject LoadPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{ShellPrefabFolder}/{name}.prefab")
            ?? throw new InvalidOperationException($"Missing shell puzzle prefab: {name}");

        private static Material RequiredMaterial(string path) =>
            AssetDatabase.LoadAssetAtPath<Material>(path)
            ?? throw new InvalidOperationException($"Missing material: {path}");

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindChild(child, name);
                if (found != null) return found;
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
