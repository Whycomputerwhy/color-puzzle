#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorPuzzle.Editor
{
    /// <summary>
    /// One-time additive composition pass for the authored underwater demo. It upgrades the shared
    /// decoy prefab, moves only the requested gameplay anchors, and adds a separately named scene
    /// group so previously hand-authored objects are never regenerated or replaced.
    /// </summary>
    public static class UnderwaterGameplayExpansion
    {
        private enum Palette
        {
            Aqua,
            DeepBlue,
            Purple,
            Orange,
            Pink,
            Green,
            Cyan,
            Gold,
            Sand
        }

        private readonly struct DecoySpec
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly float Diameter;
            public readonly Palette Color;

            public DecoySpec(string name, Vector3 position, float diameter, Palette color)
            {
                Name = name;
                Position = position;
                Diameter = diameter;
                Color = color;
            }
        }

        private readonly struct PropSpec
        {
            public readonly string Name;
            public readonly string PrefabPath;
            public readonly Vector3 Position;
            public readonly Vector3 Rotation;
            public readonly Vector3 Scale;
            public readonly Palette Color;

            public PropSpec(
                string name,
                string prefabPath,
                Vector3 position,
                Vector3 scale,
                Palette color,
                Vector3? rotation = null)
            {
                Name = name;
                PrefabPath = prefabPath;
                Position = position;
                Rotation = rotation ?? Vector3.zero;
                Scale = scale;
                Color = color;
            }
        }

        // CENTRALIZED GAMEPLAY / COMPOSITION PARAMETERS
        private static class Config
        {
            public const string ScenePath = "Assets/ColorPuzzle/Scenes/ColorPuzzleUnderwaterDemo.unity";
            public const string ExpansionRootName = "--- GAMEPLAY EXPANSION 0.2.35 ---";
            public const string DecoyGroupName = "Interactive Decoy Balls";
            public const string EnvironmentGroupName = "Expanded Environment";
            public const string SurroundingRootName = "--- SURROUNDING EXPANSION 0.2.36 ---";
            public const string SurroundingDecoyGroupName = "Perimeter Interactive Decoys";
            public const string SurroundingEnvironmentGroupName = "Perimeter Route Structures";
            public const string DecoyPrefabPath = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterDecoySphere.prefab";
            public const string BlockPrefabPath = "Assets/ColorPuzzle/Prefabs/World/WorldBlock.prefab";
            public const string RockPrefabPath = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterRock.prefab";
            public const string CoralPrefabPath = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterCoral.prefab";
            public const string KelpPrefabPath = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterKelp.prefab";
            public const string BubblesPrefabPath = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterBubbles.prefab";
            public const string DecoyObjectId = "decoy-ball";
            public const string DecoyDisplayName = "Ball";

            // The player begins close enough to read the target, while facing into the main map.
            public static readonly Vector3 PlayerPosition = new Vector3(0.1f, 0.05f, -2.8f);
            public static readonly Vector3 PlayerEulerAngles = new Vector3(0f, 34f, 0f);

            // The solution is behind the starting sightline, screened by the rear-left grotto.
            public static readonly Vector3 RedBallPosition = new Vector3(-7.55f, 1.2f, -6.15f);
            public static readonly Vector3 RedBallEulerAngles = Vector3.zero;

            public static readonly IReadOnlyDictionary<Palette, string> MaterialPaths =
                new Dictionary<Palette, string>
                {
                    { Palette.Aqua, "Assets/ColorPuzzle/Materials/Underwater/Underwater_Aqua.mat" },
                    { Palette.DeepBlue, "Assets/ColorPuzzle/Materials/Underwater/Underwater_DeepBlue.mat" },
                    { Palette.Purple, "Assets/ColorPuzzle/Materials/Underwater/Underwater_PurpleRock.mat" },
                    { Palette.Orange, "Assets/ColorPuzzle/Materials/Underwater/Underwater_CoralOrange.mat" },
                    { Palette.Pink, "Assets/ColorPuzzle/Materials/Underwater/Underwater_CoralPink.mat" },
                    { Palette.Green, "Assets/ColorPuzzle/Materials/Underwater/Underwater_KelpGreen.mat" },
                    { Palette.Cyan, "Assets/ColorPuzzle/Materials/Underwater/Underwater_BubbleCyan.mat" },
                    { Palette.Gold, "Assets/ColorPuzzle/Materials/Underwater/Underwater_TreasureGold.mat" },
                    { Palette.Sand, "Assets/ColorPuzzle/Materials/Underwater/Underwater_Sand.mat" }
                };

            // Distributed across the forward route, side routes, rear space, and elevated landmarks.
            public static readonly DecoySpec[] Decoys =
            {
                new DecoySpec("Decoy_Pink_NearGoalSide", new Vector3(-1.45f, 0.55f, 0.95f), 0.72f, Palette.Pink),
                new DecoySpec("Decoy_Gold_BehindStart", new Vector3(3.25f, 0.58f, -5.7f), 0.78f, Palette.Gold),
                new DecoySpec("Decoy_Blue_RearLeft", new Vector3(-3.15f, 0.62f, -6.85f), 0.74f, Palette.DeepBlue),
                new DecoySpec("Decoy_Green_GrottoShelf", new Vector3(-6.45f, 1.18f, -7.05f), 0.7f, Palette.Green),
                new DecoySpec("Decoy_Purple_RearRight", new Vector3(8.05f, 0.75f, -6.55f), 0.84f, Palette.Purple),
                new DecoySpec("Decoy_Orange_LeftAlcove", new Vector3(-8.35f, 0.7f, 1.8f), 0.8f, Palette.Orange),
                new DecoySpec("Decoy_Aqua_RightSlab", new Vector3(5.15f, 1.12f, 4.4f), 0.68f, Palette.Aqua),
                new DecoySpec("Decoy_Cyan_HighSteps", new Vector3(0.9f, 2.72f, 6.25f), 0.62f, Palette.Cyan),
                new DecoySpec("Decoy_Gold_FarLeft", new Vector3(-8.55f, 0.68f, 8.5f), 0.76f, Palette.Gold),
                new DecoySpec("Decoy_Pink_ArchBack", new Vector3(7.05f, 0.62f, 9.65f), 0.7f, Palette.Pink)
            };

            public static readonly PropSpec[] Props =
            {
                // Rear composition: gives the player a reason to turn around and forms an open grotto.
                new PropSpec("RearGrottoShelf", BlockPrefabPath, new Vector3(-7f, 0.35f, -6.3f), new Vector3(3.4f, 0.7f, 2.6f), Palette.Sand),
                new PropSpec("RearGrottoBack", BlockPrefabPath, new Vector3(-7.6f, 1.25f, -8.65f), new Vector3(4.8f, 2.5f, 0.8f), Palette.DeepBlue, new Vector3(0f, -6f, 0f)),
                new PropSpec("RearGrottoScreen", RockPrefabPath, new Vector3(-5.95f, 0.55f, -5.25f), new Vector3(1.55f, 1.65f, 1.3f), Palette.Purple, new Vector3(0f, 28f, 0f)),
                new PropSpec("RearStartSlab", BlockPrefabPath, new Vector3(3.2f, 0.2f, -6.7f), new Vector3(4f, 0.45f, 2.6f), Palette.Aqua, new Vector3(0f, 10f, 0f)),
                new PropSpec("RearRightRidge", BlockPrefabPath, new Vector3(8.8f, 1.1f, -7.25f), new Vector3(2.2f, 2.2f, 4.5f), Palette.DeepBlue, new Vector3(0f, -12f, -4f)),
                new PropSpec("RearCoralMarker", CoralPrefabPath, new Vector3(-4.7f, 0.15f, -7.7f), new Vector3(1.15f, 1.25f, 1.15f), Palette.Orange, new Vector3(0f, 22f, 0f)),
                new PropSpec("RearKelpA", KelpPrefabPath, new Vector3(-9f, 0f, -6.4f), new Vector3(1.05f, 1.35f, 1.05f), Palette.Green, new Vector3(0f, 10f, 0f)),
                new PropSpec("RearKelpB", KelpPrefabPath, new Vector3(6.8f, 0f, -8.1f), new Vector3(0.9f, 1.2f, 0.9f), Palette.Green, new Vector3(0f, -18f, 0f)),
                new PropSpec("RearBubbles", BubblesPrefabPath, new Vector3(4.9f, 2.1f, -7.4f), Vector3.one, Palette.Cyan),

                // Side and far-field additions break the front-only composition and create landmarks.
                new PropSpec("LeftAlcoveRock", RockPrefabPath, new Vector3(-8.7f, 0.45f, 0.4f), new Vector3(1.35f, 1.1f, 1.6f), Palette.Purple, new Vector3(0f, 40f, 0f)),
                new PropSpec("LeftAlcoveCoral", CoralPrefabPath, new Vector3(-9.2f, 0.15f, 2.5f), new Vector3(0.9f, 1.25f, 0.9f), Palette.Pink, new Vector3(0f, -25f, 0f)),
                new PropSpec("MidLeftKelp", KelpPrefabPath, new Vector3(-6.4f, 0f, 3.3f), new Vector3(0.85f, 1.15f, 0.85f), Palette.Green),
                new PropSpec("RightSlabSupport", BlockPrefabPath, new Vector3(5.25f, 0.45f, 4.4f), new Vector3(2.2f, 0.85f, 1.8f), Palette.Aqua, new Vector3(0f, -16f, 0f)),
                new PropSpec("RightRouteRock", RockPrefabPath, new Vector3(8.25f, 0.35f, 4.8f), new Vector3(1.25f, 0.95f, 1.5f), Palette.DeepBlue, new Vector3(0f, -32f, 0f)),
                new PropSpec("RightRouteCoral", CoralPrefabPath, new Vector3(9f, 0.15f, 7.5f), new Vector3(1.05f, 1.35f, 1.05f), Palette.Orange, new Vector3(0f, 18f, 0f)),
                new PropSpec("FarLeftRock", RockPrefabPath, new Vector3(-9.1f, 0.45f, 8.8f), new Vector3(1.5f, 1.2f, 1.35f), Palette.Purple, new Vector3(0f, 14f, 0f)),
                new PropSpec("FarLeftBubbles", BubblesPrefabPath, new Vector3(-8.1f, 2.4f, 7.2f), new Vector3(0.85f, 0.85f, 0.85f), Palette.Cyan),
                new PropSpec("ArchBackCoral", CoralPrefabPath, new Vector3(7.2f, 0.15f, 10.2f), new Vector3(1.2f, 1.15f, 1.2f), Palette.Pink, new Vector3(0f, 35f, 0f)),
                new PropSpec("ArchBackKelp", KelpPrefabPath, new Vector3(5.5f, 0f, 10.1f), new Vector3(1f, 1.4f, 1f), Palette.Green, new Vector3(0f, -12f, 0f)),
                new PropSpec("ArchBubbles", BubblesPrefabPath, new Vector3(8.3f, 3f, 9.7f), new Vector3(1.1f, 1.1f, 1.1f), Palette.Cyan)
            };

            // Second-pass search objects: every perimeter route has low, mid, and elevated rewards.
            public static readonly DecoySpec[] SurroundingDecoys =
            {
                new DecoySpec("Decoy_Cyan_RearLowPocket", new Vector3(1.25f, 0.72f, -5.25f), 0.68f, Palette.Cyan),
                new DecoySpec("Decoy_Orange_RearHighTerrace", new Vector3(-1.15f, 2.52f, -8.05f), 0.72f, Palette.Orange),
                new DecoySpec("Decoy_Purple_UnderRearBridge", new Vector3(2.6f, 0.62f, -8f), 0.76f, Palette.Purple),
                new DecoySpec("Decoy_Gold_RearRightPocket", new Vector3(6.45f, 1.42f, -6.05f), 0.7f, Palette.Gold),
                new DecoySpec("Decoy_Green_LeftMidTier", new Vector3(-8.2f, 1.78f, -0.35f), 0.72f, Palette.Green),
                new DecoySpec("Decoy_Pink_LeftWalkway", new Vector3(-6.25f, 2.48f, 2.55f), 0.68f, Palette.Pink),
                new DecoySpec("Decoy_Aqua_RightHighTier", new Vector3(8.25f, 2.48f, 1.55f), 0.74f, Palette.Aqua),
                new DecoySpec("Decoy_Blue_RightWalkway", new Vector3(6.35f, 2.48f, 3.05f), 0.7f, Palette.DeepBlue)
            };

            // Large-to-small construction: terrain masses, route supports, then landmark dressing.
            public static readonly PropSpec[] SurroundingProps =
            {
                // Rear terrace and bridge network.
                new PropSpec("RearTerrace_Low", BlockPrefabPath, new Vector3(0.2f, 0.25f, -5.2f), new Vector3(3.6f, 0.5f, 1.8f), Palette.Sand),
                new PropSpec("RearTerrace_Mid", BlockPrefabPath, new Vector3(-0.4f, 0.8f, -6.65f), new Vector3(3.2f, 1.1f, 1.6f), Palette.Aqua),
                new PropSpec("RearTerrace_High", BlockPrefabPath, new Vector3(-1.1f, 1.45f, -8.05f), new Vector3(2.8f, 1.5f, 1.8f), Palette.Orange),
                new PropSpec("RearBridge_Deck", BlockPrefabPath, new Vector3(2.65f, 2.05f, -8f), new Vector3(4.2f, 0.4f, 1.3f), Palette.Pink),
                new PropSpec("RearBridge_PillarLeft", BlockPrefabPath, new Vector3(1.1f, 1.05f, -8f), new Vector3(0.7f, 2.1f, 0.7f), Palette.Purple),
                new PropSpec("RearBridge_PillarRight", BlockPrefabPath, new Vector3(4.15f, 1.05f, -8f), new Vector3(0.7f, 2.1f, 0.7f), Palette.Purple),
                new PropSpec("RearRight_Shelf", BlockPrefabPath, new Vector3(6.45f, 0.55f, -6.05f), new Vector3(3f, 1.1f, 2.1f), Palette.DeepBlue, new Vector3(0f, -8f, 0f)),
                new PropSpec("RearRight_PocketWall", BlockPrefabPath, new Vector3(5.65f, 1.65f, -8.8f), new Vector3(1f, 3.3f, 3f), Palette.Purple, new Vector3(0f, 10f, 0f)),
                new PropSpec("RearRight_PocketLip", BlockPrefabPath, new Vector3(7.1f, 1.2f, -8.2f), new Vector3(2.5f, 0.5f, 1f), Palette.Aqua, new Vector3(0f, -12f, 0f)),
                new PropSpec("RearRoute_Rock", RockPrefabPath, new Vector3(4.9f, 0.4f, -5.25f), new Vector3(1.3f, 1.15f, 1.45f), Palette.Purple, new Vector3(0f, 24f, 0f)),
                new PropSpec("RearRoute_Coral", CoralPrefabPath, new Vector3(2.6f, 0.15f, -6.65f), new Vector3(1.05f, 1.35f, 1.05f), Palette.Orange, new Vector3(0f, -18f, 0f)),
                new PropSpec("RearRoute_Kelp", KelpPrefabPath, new Vector3(7.65f, 0f, -7.2f), new Vector3(0.95f, 1.4f, 0.95f), Palette.Green, new Vector3(0f, 16f, 0f)),
                new PropSpec("RearRoute_Bubbles", BubblesPrefabPath, new Vector3(3.7f, 3.05f, -8.25f), new Vector3(0.9f, 0.9f, 0.9f), Palette.Cyan),

                // Left stepped perimeter and raised walkway.
                new PropSpec("LeftTier_Low", BlockPrefabPath, new Vector3(-7.45f, 0.35f, -2.35f), new Vector3(2.6f, 0.7f, 2.2f), Palette.Sand, new Vector3(0f, 8f, 0f)),
                new PropSpec("LeftTier_Mid", BlockPrefabPath, new Vector3(-8.15f, 0.9f, -0.35f), new Vector3(2.4f, 1.1f, 2f), Palette.DeepBlue, new Vector3(0f, -6f, 0f)),
                new PropSpec("LeftTier_High", BlockPrefabPath, new Vector3(-8.35f, 1.55f, 1.55f), new Vector3(2.2f, 1.3f, 2f), Palette.Purple, new Vector3(0f, 10f, 0f)),
                new PropSpec("LeftWalkway_Deck", BlockPrefabPath, new Vector3(-6.25f, 2.05f, 2.55f), new Vector3(3.5f, 0.4f, 1.2f), Palette.Pink, new Vector3(0f, 6f, 0f)),
                new PropSpec("LeftWalkway_Pillar", BlockPrefabPath, new Vector3(-5.05f, 1.05f, 2.55f), new Vector3(0.7f, 2.1f, 0.7f), Palette.Purple),
                new PropSpec("LeftNiche_Wall", BlockPrefabPath, new Vector3(-9.2f, 1.45f, 4.1f), new Vector3(1f, 2.9f, 3f), Palette.DeepBlue, new Vector3(0f, -8f, 0f)),
                new PropSpec("LeftNiche_Rock", RockPrefabPath, new Vector3(-6.7f, 0.35f, 0.8f), new Vector3(1.15f, 0.95f, 1.35f), Palette.Purple, new Vector3(0f, 35f, 0f)),
                new PropSpec("LeftNiche_Coral", CoralPrefabPath, new Vector3(-7.15f, 0.15f, 4.15f), new Vector3(0.95f, 1.25f, 0.95f), Palette.Pink, new Vector3(0f, 15f, 0f)),
                new PropSpec("LeftNiche_Kelp", KelpPrefabPath, new Vector3(-9.3f, 0f, -1.8f), new Vector3(1f, 1.35f, 1f), Palette.Green, new Vector3(0f, -12f, 0f)),
                new PropSpec("LeftNiche_Bubbles", BubblesPrefabPath, new Vector3(-7.6f, 3f, 2.9f), new Vector3(0.8f, 0.8f, 0.8f), Palette.Cyan),

                // Right stepped perimeter and overlook.
                new PropSpec("RightTier_Low", BlockPrefabPath, new Vector3(7.2f, 0.35f, -2f), new Vector3(2.5f, 0.7f, 2f), Palette.Sand, new Vector3(0f, -10f, 0f)),
                new PropSpec("RightTier_Mid", BlockPrefabPath, new Vector3(7.95f, 0.9f, -0.15f), new Vector3(2.35f, 1.1f, 1.9f), Palette.Aqua, new Vector3(0f, 7f, 0f)),
                new PropSpec("RightTier_High", BlockPrefabPath, new Vector3(8.25f, 1.55f, 1.55f), new Vector3(2.2f, 1.3f, 2f), Palette.Orange, new Vector3(0f, -8f, 0f)),
                new PropSpec("RightWalkway_Deck", BlockPrefabPath, new Vector3(6.35f, 2.05f, 3.05f), new Vector3(4f, 0.4f, 1.2f), Palette.Pink, new Vector3(0f, -5f, 0f)),
                new PropSpec("RightWalkway_PillarA", BlockPrefabPath, new Vector3(5.05f, 1.05f, 3.05f), new Vector3(0.7f, 2.1f, 0.7f), Palette.Purple),
                new PropSpec("RightWalkway_PillarB", BlockPrefabPath, new Vector3(7.75f, 1.05f, 3.05f), new Vector3(0.7f, 2.1f, 0.7f), Palette.Purple),
                new PropSpec("RightNiche_Wall", BlockPrefabPath, new Vector3(9.15f, 1.5f, 4.8f), new Vector3(1f, 3f, 3.2f), Palette.DeepBlue, new Vector3(0f, 8f, 0f)),
                new PropSpec("RightNiche_Rock", RockPrefabPath, new Vector3(6.8f, 0.4f, 5.15f), new Vector3(1.3f, 1.05f, 1.5f), Palette.Purple, new Vector3(0f, -28f, 0f)),
                new PropSpec("RightNiche_Coral", CoralPrefabPath, new Vector3(8.5f, 0.15f, 5.8f), new Vector3(1.05f, 1.3f, 1.05f), Palette.Orange, new Vector3(0f, -20f, 0f)),
                new PropSpec("RightNiche_Kelp", KelpPrefabPath, new Vector3(9.25f, 0f, -1.35f), new Vector3(0.95f, 1.45f, 0.95f), Palette.Green, new Vector3(0f, 15f, 0f)),
                new PropSpec("RightNiche_Bubbles", BubblesPrefabPath, new Vector3(7.2f, 3.15f, 3.65f), new Vector3(0.9f, 0.9f, 0.9f), Palette.Cyan)
            };
        }

        [MenuItem("Color Puzzle/Expand Underwater Gameplay (Additive)")]
        public static void ExpandUnderwaterGameplay()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Config.ScenePath)
                throw new InvalidOperationException($"Open {Config.ScenePath} before running this additive pass. Nothing was changed.");
            if (FindSceneObject(Config.ExpansionRootName, scene) != null)
            {
                Debug.Log("The underwater gameplay expansion already exists. Nothing was overwritten.");
                return;
            }

            UpgradeDecoyPrefab();
            Dictionary<Palette, Material> materials = LoadMaterials();
            GameObject decoyPrefab = LoadRequiredAsset<GameObject>(Config.DecoyPrefabPath);

            FirstPersonPlayer player = UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>(FindObjectsInactive.Include);
            GoalPedestal goal = UnityEngine.Object.FindAnyObjectByType<GoalPedestal>(FindObjectsInactive.Include);
            DiscoverableObject redBall = FindRedBall();
            if (player == null || goal == null || redBall == null)
                throw new InvalidOperationException("The player, goal, or red ball could not be found. Nothing was changed.");

            MoveGameplayAnchors(player, redBall);

            GameObject root = new GameObject(Config.ExpansionRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            GameObject decoyGroup = CreateGroup(Config.DecoyGroupName, root.transform, scene);
            GameObject environmentGroup = CreateGroup(Config.EnvironmentGroupName, root.transform, scene);

            PopulateDecoys(scene, decoyGroup.transform, decoyPrefab, materials, Config.Decoys);
            PopulateEnvironment(scene, environmentGroup.transform, materials, Config.Props);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, Config.ScenePath))
                throw new InvalidOperationException("Unity could not save the additive underwater gameplay pass.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log("Expanded the underwater demo additively: nearby start, interactive decoys, wrong-ball goal testing, rear-space composition, scattered objects, and a relocated hidden red ball.");
        }

        [MenuItem("Color Puzzle/Deepen Underwater Surroundings (Additive)")]
        public static void DeepenUnderwaterSurroundings()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Config.ScenePath)
                throw new InvalidOperationException($"Open {Config.ScenePath} before running this additive pass. Nothing was changed.");
            if (FindSceneObject(Config.SurroundingRootName, scene) != null)
            {
                Debug.Log("The surrounding underwater expansion already exists. Nothing was overwritten.");
                return;
            }

            UpgradeDecoyPrefab();
            Dictionary<Palette, Material> materials = LoadMaterials();
            GameObject decoyPrefab = LoadRequiredAsset<GameObject>(Config.DecoyPrefabPath);
            GameObject root = new GameObject(Config.SurroundingRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            GameObject decoyGroup = CreateGroup(Config.SurroundingDecoyGroupName, root.transform, scene);
            GameObject environmentGroup = CreateGroup(Config.SurroundingEnvironmentGroupName, root.transform, scene);

            PopulateDecoys(scene, decoyGroup.transform, decoyPrefab, materials, Config.SurroundingDecoys);
            PopulateEnvironment(scene, environmentGroup.transform, materials, Config.SurroundingProps);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, Config.ScenePath))
                throw new InvalidOperationException("Unity could not save the additive surrounding-map pass.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log("Deepened the underwater perimeter with rear terraces, bridges, side walkways, vertical landmarks, hiding pockets, and eight more interactive decoys.");
        }

        private static void UpgradeDecoyPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Config.DecoyPrefabPath);
            try
            {
                Renderer baseRenderer = root.GetComponent<Renderer>();
                Collider collider = root.GetComponent<Collider>();
                Transform proxyTransform = FindChild(root.transform, "ColorProxy");
                if (baseRenderer == null || collider == null || proxyTransform == null)
                    throw new InvalidOperationException("The decoy prefab is missing its renderer, collider, or ColorProxy child.");

                Rigidbody body = root.GetComponent<Rigidbody>();
                if (body == null) body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.None;

                DiscoverableObject discoverable = root.GetComponent<DiscoverableObject>();
                if (discoverable == null) discoverable = root.AddComponent<DiscoverableObject>();
                SerializedObject serialized = new SerializedObject(discoverable);
                serialized.FindProperty("objectId").stringValue = Config.DecoyObjectId;
                serialized.FindProperty("displayName").stringValue = Config.DecoyDisplayName;
                serialized.FindProperty("grayscaleRenderer").objectReferenceValue = baseRenderer;
                serialized.FindProperty("discoveredMaterial").objectReferenceValue = null;
                serialized.FindProperty("colorProxy").objectReferenceValue = proxyTransform.gameObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, Config.DecoyPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void MoveGameplayAnchors(FirstPersonPlayer player, DiscoverableObject redBall)
        {
            player.transform.SetPositionAndRotation(Config.PlayerPosition, Quaternion.Euler(Config.PlayerEulerAngles));
            redBall.transform.SetPositionAndRotation(Config.RedBallPosition, Quaternion.Euler(Config.RedBallEulerAngles));
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(redBall.transform);
        }

        private static void PopulateDecoys(
            Scene scene,
            Transform parent,
            GameObject prefab,
            IReadOnlyDictionary<Palette, Material> materials,
            IReadOnlyList<DecoySpec> specs)
        {
            foreach (DecoySpec spec in specs)
            {
                GameObject instance = InstantiatePrefab(prefab, scene, parent);
                instance.name = spec.Name;
                instance.transform.SetPositionAndRotation(spec.Position, Quaternion.identity);
                instance.transform.localScale = Vector3.one * spec.Diameter;
                AssignRevealMaterial(instance, materials[spec.Color]);
            }
        }

        private static void PopulateEnvironment(
            Scene scene,
            Transform parent,
            IReadOnlyDictionary<Palette, Material> materials,
            IReadOnlyList<PropSpec> specs)
        {
            Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            foreach (PropSpec spec in specs)
            {
                if (!prefabs.TryGetValue(spec.PrefabPath, out GameObject prefab))
                {
                    prefab = LoadRequiredAsset<GameObject>(spec.PrefabPath);
                    prefabs.Add(spec.PrefabPath, prefab);
                }

                GameObject instance = InstantiatePrefab(prefab, scene, parent);
                instance.name = spec.Name;
                instance.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(spec.Rotation));
                instance.transform.localScale = spec.Scale;
                AssignRevealMaterial(instance, materials[spec.Color]);
            }
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Scene scene, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException($"Could not instantiate prefab {prefab.name}.");
            instance.transform.SetParent(parent, true);
            return instance;
        }

        private static void AssignRevealMaterial(GameObject instance, Material material)
        {
            int revealLayer = LayerMask.NameToLayer("ColorReveal");
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.layer == revealLayer ||
                    renderer.name.IndexOf("ColorProxy", StringComparison.OrdinalIgnoreCase) >= 0)
                    renderer.sharedMaterial = material;
            }
        }

        private static Dictionary<Palette, Material> LoadMaterials()
        {
            Dictionary<Palette, Material> materials = new Dictionary<Palette, Material>();
            foreach (KeyValuePair<Palette, string> pair in Config.MaterialPaths)
                materials.Add(pair.Key, LoadRequiredAsset<Material>(pair.Value));
            return materials;
        }

        private static DiscoverableObject FindRedBall()
        {
            foreach (DiscoverableObject item in UnityEngine.Object.FindObjectsByType<DiscoverableObject>())
            {
                if (item.ObjectId == "red-ball") return item;
            }
            return null;
        }

        private static GameObject CreateGroup(string name, Transform parent, Scene scene)
        {
            GameObject group = new GameObject(name);
            SceneManager.MoveGameObjectToScene(group, scene);
            group.transform.SetParent(parent, false);
            return group;
        }

        private static GameObject FindSceneObject(string name, Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindChild(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Required asset is missing: {path}");
            return asset;
        }
    }
}
#endif
