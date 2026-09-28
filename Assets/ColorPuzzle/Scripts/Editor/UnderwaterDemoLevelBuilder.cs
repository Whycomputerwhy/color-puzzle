#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ColorPuzzle.Editor
{
    /// <summary>
    /// One-time, non-overwriting creator for the second demo scene. It copies the authored first
    /// level, edits only the copy, creates new underwater-only assets, and refuses to overwrite an
    /// existing destination scene.
    /// </summary>
    public static class UnderwaterDemoLevelBuilder
    {
        private enum Palette
        {
            Sand,
            Aqua,
            DeepBlue,
            PurpleRock,
            CoralOrange,
            CoralPink,
            KelpGreen,
            BubbleCyan,
            TreasureGold
        }

        private readonly struct BlockSpec
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Rotation;
            public readonly Vector3 Scale;
            public readonly Palette Palette;

            public BlockSpec(string name, Vector3 position, Vector3 scale, Palette palette, Vector3? rotation = null)
            {
                Name = name;
                Position = position;
                Rotation = rotation ?? Vector3.zero;
                Scale = scale;
                Palette = palette;
            }
        }

        private readonly struct InstanceSpec
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Rotation;
            public readonly Vector3 Scale;

            public InstanceSpec(string name, Vector3 position, Vector3 scale, Vector3? rotation = null)
            {
                Name = name;
                Position = position;
                Rotation = rotation ?? Vector3.zero;
                Scale = scale;
            }
        }

        // CENTRALIZED LEVEL / VISUAL PARAMETERS
        private static class Config
        {
            public const string SourceScene = "Assets/ColorPuzzle/Scenes/ColorPuzzleVerticalSlice.unity";
            public const string DestinationScene = "Assets/ColorPuzzle/Scenes/ColorPuzzleUnderwaterDemo.unity";
            public const string MaterialFolder = "Assets/ColorPuzzle/Materials/Underwater";
            public const string PrefabFolder = "Assets/ColorPuzzle/Prefabs/Underwater";
            public const string WorldBlockPrefab = "Assets/ColorPuzzle/Prefabs/World/WorldBlock.prefab";
            public const string WorldGrayMaterial = "Assets/ColorPuzzle/Materials/World_Gray.mat";
            public const string AdditionRootName = "--- UNDERWATER GREYBOX ADDITIONS ---";
            public const string SceneRootName = "ColorPuzzleUnderwaterDemo";
            public const float PlayerGravity = -10f;
            public const float FogStartDistance = 8f;
            public const float FogEndDistance = 42f;
            public static readonly Color FogColor = new Color(0.035f, 0.22f, 0.34f, 1f);
            public static readonly Color AmbientColor = new Color(0.16f, 0.32f, 0.38f, 1f);
            public static readonly Color RegularSkyColor = new Color(0.12f, 0.3f, 0.36f, 1f);
            public static readonly Color ColorSkyColor = new Color(0.025f, 0.3f, 0.52f, 1f);

            public static readonly IReadOnlyDictionary<Palette, Color> Colors = new Dictionary<Palette, Color>
            {
                { Palette.Sand, new Color(0.67f, 0.64f, 0.38f, 1f) },
                { Palette.Aqua, new Color(0.12f, 0.62f, 0.58f, 1f) },
                { Palette.DeepBlue, new Color(0.08f, 0.25f, 0.58f, 1f) },
                { Palette.PurpleRock, new Color(0.34f, 0.19f, 0.52f, 1f) },
                { Palette.CoralOrange, new Color(0.96f, 0.34f, 0.15f, 1f) },
                { Palette.CoralPink, new Color(0.92f, 0.18f, 0.48f, 1f) },
                { Palette.KelpGreen, new Color(0.09f, 0.5f, 0.24f, 1f) },
                { Palette.BubbleCyan, new Color(0.38f, 0.9f, 1f, 1f) },
                { Palette.TreasureGold, new Color(1f, 0.72f, 0.08f, 1f) }
            };

            public static readonly BlockSpec[] Blocks =
            {
                new BlockSpec("SeafloorShelf_Left", new Vector3(-7.5f, -0.15f, -4f), new Vector3(6f, 0.7f, 7f), Palette.Sand),
                new BlockSpec("SeafloorShelf_Right", new Vector3(7f, -0.2f, -3f), new Vector3(7f, 0.6f, 7f), Palette.Sand),
                new BlockSpec("SeafloorShelf_Back", new Vector3(0f, -0.1f, 8.5f), new Vector3(12f, 0.8f, 4f), Palette.Aqua),
                new BlockSpec("CanyonRidge_Left_A", new Vector3(-9f, 1f, 1f), new Vector3(2f, 2.5f, 6f), Palette.DeepBlue, new Vector3(0f, 8f, 4f)),
                new BlockSpec("CanyonRidge_Left_B", new Vector3(-8.2f, 1.7f, 7.5f), new Vector3(3f, 3.5f, 4f), Palette.PurpleRock, new Vector3(0f, -12f, -3f)),
                new BlockSpec("CanyonRidge_Right_A", new Vector3(8.8f, 0.8f, -1f), new Vector3(2.2f, 2.1f, 5.5f), Palette.DeepBlue, new Vector3(0f, -8f, -5f)),
                new BlockSpec("CanyonRidge_Right_B", new Vector3(8.4f, 1.5f, 7.2f), new Vector3(3f, 3.2f, 4f), Palette.PurpleRock, new Vector3(0f, 14f, 4f)),
                new BlockSpec("ReefStep_A", new Vector3(-2.8f, 0.55f, 2.5f), new Vector3(3.2f, 1.1f, 2.2f), Palette.Aqua),
                new BlockSpec("ReefStep_B", new Vector3(-1f, 1.2f, 4.6f), new Vector3(2.8f, 1f, 2.2f), Palette.CoralOrange),
                new BlockSpec("ReefStep_C", new Vector3(1.1f, 1.85f, 6.4f), new Vector3(3f, 0.9f, 2.2f), Palette.CoralPink),
                new BlockSpec("SunkenSlab_A", new Vector3(4.8f, 0.35f, 1.5f), new Vector3(3.8f, 0.7f, 2f), Palette.DeepBlue, new Vector3(0f, 18f, 0f)),
                new BlockSpec("SunkenSlab_B", new Vector3(5.7f, 0.75f, 5f), new Vector3(3.2f, 0.6f, 1.7f), Palette.Aqua, new Vector3(0f, -15f, 6f)),
                new BlockSpec("ArchPillar_Left", new Vector3(5.6f, 2f, 8.8f), new Vector3(0.9f, 4f, 0.9f), Palette.PurpleRock),
                new BlockSpec("ArchPillar_Right", new Vector3(8.2f, 2f, 8.8f), new Vector3(0.9f, 4f, 0.9f), Palette.PurpleRock),
                new BlockSpec("ArchLintel", new Vector3(6.9f, 4.15f, 8.8f), new Vector3(3.5f, 0.7f, 1f), Palette.CoralOrange),
                new BlockSpec("FloatingReef_A", new Vector3(-5.5f, 3.5f, 2f), new Vector3(2.5f, 0.65f, 2f), Palette.Aqua, new Vector3(0f, 12f, -4f)),
                new BlockSpec("FloatingReef_B", new Vector3(3.8f, 3.2f, 7f), new Vector3(2.2f, 0.55f, 1.8f), Palette.CoralPink, new Vector3(0f, -20f, 5f)),
                new BlockSpec("TreasureMarker_A", new Vector3(-7f, 0.45f, 4.2f), new Vector3(0.7f, 0.7f, 0.7f), Palette.TreasureGold, new Vector3(0f, 18f, 0f)),
                new BlockSpec("TreasureMarker_B", new Vector3(7.2f, 0.35f, -5.5f), new Vector3(0.55f, 0.55f, 0.55f), Palette.TreasureGold, new Vector3(12f, 30f, 0f)),
                new BlockSpec("SmallStone_A", new Vector3(-3.8f, 0.25f, -5.8f), new Vector3(0.8f, 0.5f, 1.1f), Palette.PurpleRock, new Vector3(8f, 22f, 4f)),
                new BlockSpec("SmallStone_B", new Vector3(2.8f, 0.22f, -6f), new Vector3(1.1f, 0.45f, 0.7f), Palette.DeepBlue, new Vector3(-5f, -18f, 9f)),
                new BlockSpec("SmallStone_C", new Vector3(0f, 0.18f, 9.5f), new Vector3(0.65f, 0.4f, 0.9f), Palette.PurpleRock, new Vector3(12f, 38f, 0f))
            };

            public static readonly InstanceSpec[] Rocks =
            {
                new InstanceSpec("RockCluster_A", new Vector3(-8f, 0.3f, -6f), new Vector3(1.4f, 1.1f, 1.2f), new Vector3(0f, 18f, 0f)),
                new InstanceSpec("RockCluster_B", new Vector3(8f, 0.25f, 3f), new Vector3(1.1f, 0.9f, 1.3f), new Vector3(0f, -28f, 0f)),
                new InstanceSpec("RockCluster_C", new Vector3(-6.5f, 0.4f, 9f), new Vector3(1.7f, 1.3f, 1.4f), new Vector3(0f, 42f, 0f)),
                new InstanceSpec("RockCluster_D", new Vector3(3f, 0.25f, 10f), new Vector3(0.9f, 0.8f, 1.1f), new Vector3(0f, 8f, 0f))
            };

            public static readonly InstanceSpec[] Corals =
            {
                new InstanceSpec("Coral_A", new Vector3(-8f, 0.15f, 3f), Vector3.one, new Vector3(0f, 15f, 0f)),
                new InstanceSpec("Coral_B", new Vector3(-4.8f, 0.15f, 7.8f), new Vector3(1.2f, 1.15f, 1.2f), new Vector3(0f, -30f, 0f)),
                new InstanceSpec("Coral_C", new Vector3(7f, 0.15f, 6f), new Vector3(0.9f, 1.3f, 0.9f), new Vector3(0f, 35f, 0f)),
                new InstanceSpec("Coral_D", new Vector3(5.8f, 0.15f, -5f), new Vector3(0.85f, 0.9f, 0.85f), new Vector3(0f, -12f, 0f)),
                new InstanceSpec("Coral_E", new Vector3(-1f, 0.15f, 9.2f), new Vector3(1.25f, 1.1f, 1.25f), new Vector3(0f, 55f, 0f))
            };

            public static readonly InstanceSpec[] Kelp =
            {
                new InstanceSpec("Kelp_A", new Vector3(-9.1f, 0f, -3f), Vector3.one),
                new InstanceSpec("Kelp_B", new Vector3(-7.8f, 0f, 0f), new Vector3(1.1f, 1.3f, 1.1f), new Vector3(0f, 20f, 0f)),
                new InstanceSpec("Kelp_C", new Vector3(-4.2f, 0f, 10f), new Vector3(0.9f, 1.2f, 0.9f), new Vector3(0f, -20f, 0f)),
                new InstanceSpec("Kelp_D", new Vector3(8.8f, 0f, -4f), new Vector3(1.2f, 1.1f, 1.2f), new Vector3(0f, 32f, 0f)),
                new InstanceSpec("Kelp_E", new Vector3(9f, 0f, 4.5f), Vector3.one, new Vector3(0f, -15f, 0f)),
                new InstanceSpec("Kelp_F", new Vector3(3.8f, 0f, 9.8f), new Vector3(0.9f, 1.35f, 0.9f), new Vector3(0f, 10f, 0f))
            };

            public static readonly InstanceSpec[] Bubbles =
            {
                new InstanceSpec("Bubbles_A", new Vector3(-6.8f, 2.2f, -1f), Vector3.one),
                new InstanceSpec("Bubbles_B", new Vector3(7.5f, 2.8f, 2f), new Vector3(1.2f, 1.2f, 1.2f)),
                new InstanceSpec("Bubbles_C", new Vector3(-2f, 3.5f, 8.5f), new Vector3(0.9f, 0.9f, 0.9f)),
                new InstanceSpec("Bubbles_D", new Vector3(4.5f, 2f, -4f), new Vector3(0.75f, 0.75f, 0.75f))
            };
        }

        [MenuItem("Color Puzzle/Create Underwater Demo Level (New Scene Only)")]
        public static void CreateUnderwaterDemoLevel()
        {
            if (File.Exists(Config.DestinationScene))
                throw new InvalidOperationException(
                    $"Underwater scene already exists at {Config.DestinationScene}. Nothing was overwritten.");
            if (!File.Exists(Config.SourceScene))
                throw new FileNotFoundException("The authored source demo scene is missing. Nothing was changed.", Config.SourceScene);

            EnsureFolder(Config.MaterialFolder);
            EnsureFolder(Config.PrefabFolder);
            Dictionary<Palette, Material> palette = CreatePalette();
            Material grayMaterial = AssetDatabase.LoadAssetAtPath<Material>(Config.WorldGrayMaterial);
            if (grayMaterial == null) throw new FileNotFoundException("World gray material is missing.", Config.WorldGrayMaterial);

            Dictionary<string, GameObject> prefabs = CreateUnderwaterPrefabs(grayMaterial, palette);
            if (!AssetDatabase.CopyAsset(Config.SourceScene, Config.DestinationScene))
                throw new IOException("Unity could not copy the source scene. Nothing in the source scene was changed.");

            AssetDatabase.ImportAsset(Config.DestinationScene, ImportAssetOptions.ForceUpdate);
            Scene scene = EditorSceneManager.OpenScene(Config.DestinationScene, OpenSceneMode.Single);
            RenameSceneRoot(scene);
            ApplyUnderwaterEnvironment();
            ApplyLevelSpecificPlayerSettings();

            GameObject additions = new GameObject(Config.AdditionRootName);
            SceneManager.MoveGameObjectToScene(additions, scene);
            PopulateBlocks(scene, additions.transform, palette);
            PopulateInstances(scene, additions.transform, prefabs);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, Config.DestinationScene))
                throw new IOException("Unity could not save the underwater scene copy.");
            AppendSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = additions;
            Debug.Log($"Created underwater demo level at {Config.DestinationScene}. The original scene was not modified.");
        }

        private static Dictionary<Palette, Material> CreatePalette()
        {
            Dictionary<Palette, Material> materials = new Dictionary<Palette, Material>();
            foreach (KeyValuePair<Palette, Color> entry in Config.Colors)
            {
                string path = $"{Config.MaterialFolder}/Underwater_{entry.Key}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) throw new InvalidOperationException("No supported Lit shader is available.");
                    material = new Material(shader) { name = $"Underwater_{entry.Key}", color = entry.Value };
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", entry.Value);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
                    AssetDatabase.CreateAsset(material, path);
                }
                materials.Add(entry.Key, material);
            }
            return materials;
        }

        private static Dictionary<string, GameObject> CreateUnderwaterPrefabs(Material gray, Dictionary<Palette, Material> palette)
        {
            return new Dictionary<string, GameObject>
            {
                { "Rock", CreatePrefabIfMissing("UnderwaterRock", root => BuildRock(root, gray, palette[Palette.PurpleRock])) },
                { "Coral", CreatePrefabIfMissing("UnderwaterCoral", root => BuildCoral(root, gray, palette[Palette.CoralPink], palette[Palette.CoralOrange])) },
                { "Kelp", CreatePrefabIfMissing("UnderwaterKelp", root => BuildKelp(root, gray, palette[Palette.KelpGreen])) },
                { "Bubbles", CreatePrefabIfMissing("UnderwaterBubbles", root => BuildBubbles(root, gray, palette[Palette.BubbleCyan])) }
            };
        }

        private static GameObject CreatePrefabIfMissing(string name, Action<Transform> build)
        {
            string path = $"{Config.PrefabFolder}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject root = new GameObject(name);
            build(root.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildRock(Transform root, Material gray, Material color)
        {
            CreateColoredPrimitive(root, "RockLarge", PrimitiveType.Sphere, new Vector3(-0.35f, 0.55f, 0f), new Vector3(1.3f, 1.05f, 1.1f), new Vector3(0f, 18f, -8f), gray, color, true);
            CreateColoredPrimitive(root, "RockMedium", PrimitiveType.Sphere, new Vector3(0.55f, 0.38f, 0.2f), new Vector3(0.9f, 0.75f, 0.85f), new Vector3(8f, -20f, 12f), gray, color, true);
            CreateColoredPrimitive(root, "RockSmall", PrimitiveType.Sphere, new Vector3(0.15f, 0.24f, -0.65f), new Vector3(0.65f, 0.48f, 0.6f), new Vector3(-10f, 35f, 0f), gray, color, true);
        }

        private static void BuildCoral(Transform root, Material gray, Material pink, Material orange)
        {
            CreateColoredPrimitive(root, "CoralStem", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f), new Vector3(0.22f, 0.8f, 0.22f), Vector3.zero, gray, pink, false);
            CreateColoredPrimitive(root, "CoralBranchLeft", PrimitiveType.Cylinder, new Vector3(-0.35f, 1.15f, 0f), new Vector3(0.16f, 0.58f, 0.16f), new Vector3(0f, 0f, 38f), gray, orange, false);
            CreateColoredPrimitive(root, "CoralBranchRight", PrimitiveType.Cylinder, new Vector3(0.38f, 1.25f, 0.05f), new Vector3(0.14f, 0.65f, 0.14f), new Vector3(8f, 0f, -42f), gray, pink, false);
            CreateColoredPrimitive(root, "CoralTip", PrimitiveType.Sphere, new Vector3(0f, 1.65f, 0f), new Vector3(0.42f, 0.42f, 0.42f), Vector3.zero, gray, orange, false);
        }

        private static void BuildKelp(Transform root, Material gray, Material color)
        {
            CreateColoredPrimitive(root, "KelpBladeA", PrimitiveType.Capsule, new Vector3(-0.3f, 1.15f, 0f), new Vector3(0.18f, 1.15f, 0.18f), new Vector3(0f, 0f, -8f), gray, color, false);
            CreateColoredPrimitive(root, "KelpBladeB", PrimitiveType.Capsule, new Vector3(0f, 1.45f, 0.1f), new Vector3(0.2f, 1.45f, 0.2f), new Vector3(7f, 0f, 5f), gray, color, false);
            CreateColoredPrimitive(root, "KelpBladeC", PrimitiveType.Capsule, new Vector3(0.32f, 1f, -0.08f), new Vector3(0.16f, 1f, 0.16f), new Vector3(-6f, 0f, 11f), gray, color, false);
        }

        private static void BuildBubbles(Transform root, Material gray, Material color)
        {
            CreateColoredPrimitive(root, "BubbleA", PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), Vector3.one * 0.24f, Vector3.zero, gray, color, false);
            CreateColoredPrimitive(root, "BubbleB", PrimitiveType.Sphere, new Vector3(0.32f, 0.58f, 0.05f), Vector3.one * 0.17f, Vector3.zero, gray, color, false);
            CreateColoredPrimitive(root, "BubbleC", PrimitiveType.Sphere, new Vector3(-0.2f, 1.05f, 0f), Vector3.one * 0.13f, Vector3.zero, gray, color, false);
            CreateColoredPrimitive(root, "BubbleD", PrimitiveType.Sphere, new Vector3(0.12f, 1.48f, -0.04f), Vector3.one * 0.1f, Vector3.zero, gray, color, false);
        }

        private static void CreateColoredPrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 rotation, Material gray, Material color, bool collider)
        {
            GameObject baseObject = GameObject.CreatePrimitive(type);
            baseObject.name = name;
            baseObject.transform.SetParent(parent, false);
            baseObject.transform.localPosition = position;
            baseObject.transform.localEulerAngles = rotation;
            baseObject.transform.localScale = scale;
            baseObject.GetComponent<Renderer>().sharedMaterial = gray;
            if (!collider)
            {
                Collider baseCollider = baseObject.GetComponent<Collider>();
                if (baseCollider != null) UnityEngine.Object.DestroyImmediate(baseCollider);
            }

            GameObject proxy = GameObject.CreatePrimitive(type);
            proxy.name = "ColorProxy";
            proxy.transform.SetParent(baseObject.transform, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one * 1.002f;
            int revealLayer = LayerMask.NameToLayer("ColorReveal");
            if (revealLayer >= 0) proxy.layer = revealLayer;
            proxy.GetComponent<Renderer>().sharedMaterial = color;
            Collider proxyCollider = proxy.GetComponent<Collider>();
            if (proxyCollider != null) UnityEngine.Object.DestroyImmediate(proxyCollider);
        }

        private static void PopulateBlocks(Scene scene, Transform parent, Dictionary<Palette, Material> palette)
        {
            GameObject blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Config.WorldBlockPrefab);
            if (blockPrefab == null) throw new FileNotFoundException("WorldBlock prefab is missing.", Config.WorldBlockPrefab);

            foreach (BlockSpec spec in Config.Blocks)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab, scene);
                instance.name = spec.Name;
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(spec.Rotation));
                instance.transform.localScale = spec.Scale;
                ApplyBlockMaterials(instance, palette[spec.Palette]);
            }
        }

        private static void PopulateInstances(Scene scene, Transform parent, Dictionary<string, GameObject> prefabs)
        {
            PopulatePrefabSet(scene, parent, prefabs["Rock"], Config.Rocks);
            PopulatePrefabSet(scene, parent, prefabs["Coral"], Config.Corals);
            PopulatePrefabSet(scene, parent, prefabs["Kelp"], Config.Kelp);
            PopulatePrefabSet(scene, parent, prefabs["Bubbles"], Config.Bubbles);
        }

        private static void PopulatePrefabSet(Scene scene, Transform parent, GameObject prefab, IEnumerable<InstanceSpec> specs)
        {
            foreach (InstanceSpec spec in specs)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = spec.Name;
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(spec.Rotation));
                instance.transform.localScale = spec.Scale;
            }
        }

        private static void ApplyBlockMaterials(GameObject block, Material color)
        {
            int revealLayer = LayerMask.NameToLayer("ColorReveal");
            Material gray = AssetDatabase.LoadAssetAtPath<Material>(Config.WorldGrayMaterial);
            foreach (Renderer renderer in block.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = renderer.gameObject.layer == revealLayer ? color : gray;
        }

        private static void ApplyUnderwaterEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Config.FogColor;
            RenderSettings.fogStartDistance = Config.FogStartDistance;
            RenderSettings.fogEndDistance = Config.FogEndDistance;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Config.AmbientColor;
        }

        private static void ApplyLevelSpecificPlayerSettings()
        {
            FirstPersonPlayer player = UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>(FindObjectsInactive.Include);
            if (player != null)
            {
                SerializedObject serializedPlayer = new SerializedObject(player);
                serializedPlayer.FindProperty("gravity").floatValue = Config.PlayerGravity;
                serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            }

            MagnifierView magnifier = UnityEngine.Object.FindAnyObjectByType<MagnifierView>(FindObjectsInactive.Include);
            if (magnifier != null)
            {
                SerializedObject serializedMagnifier = new SerializedObject(magnifier);
                serializedMagnifier.FindProperty("regularViewSkyColor").colorValue = Config.RegularSkyColor;
                serializedMagnifier.FindProperty("revealViewSkyColor").colorValue = Config.ColorSkyColor;
                serializedMagnifier.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(magnifier);
            }
        }

        private static void RenameSceneRoot(Scene scene)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == "ColorPuzzleVerticalSlice");
            if (root != null) root.name = Config.SceneRootName;
        }

        private static void AppendSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == Config.DestinationScene)) return;
            scenes.Add(new EditorBuildSettingsScene(Config.DestinationScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folder)
        {
            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
#endif
