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
    /// One-time, targeted refinement for the authored underwater scene. It never opens or edits the
    /// original level, and it refuses to run after the named decoy-sphere group has been created.
    /// </summary>
    public static class UnderwaterSceneRefinement
    {
        private enum SphereColor
        {
            Aqua,
            DeepBlue,
            PurpleRock,
            CoralOrange,
            CoralPink,
            KelpGreen,
            BubbleCyan,
            TreasureGold
        }

        private readonly struct SphereSpec
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly float Diameter;
            public readonly SphereColor Color;

            public SphereSpec(string name, Vector3 position, float diameter, SphereColor color)
            {
                Name = name;
                Position = position;
                Diameter = diameter;
                Color = color;
            }
        }

        // CENTRALIZED UNDERWATER REFINEMENT PARAMETERS
        private static class Config
        {
            public const string UnderwaterScene = "Assets/ColorPuzzle/Scenes/ColorPuzzleUnderwaterDemo.unity";
            public const string AdditionsRootName = "--- UNDERWATER GREYBOX ADDITIONS ---";
            public const string SphereGroupName = "Underwater Decoy Spheres (Non-Interactive)";
            public const string SourceGoalPrefab = "Assets/ColorPuzzle/Prefabs/Gameplay/RedGoalPedestal.prefab";
            public const string UnderwaterGoalPrefab = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterCylinderGoal.prefab";
            public const string DecoySpherePrefab = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterDecoySphere.prefab";
            public const string WorldGrayMaterial = "Assets/ColorPuzzle/Materials/World_Gray.mat";
            public const float PlayerGravity = -8f;
            public static readonly Vector3 GoalScale = new Vector3(2.2f, 0.5f, 2.2f);
            public static readonly Vector3 GoalPlacementSocketLocalPosition = new Vector3(0f, 1.7f, 0f);
            public static readonly Vector3 GoalTargetLocalPosition = new Vector3(0f, 1.1f, 0f);
            public static readonly Vector3 GoalTargetLocalScale = new Vector3(0.28f, 0.025f, 0.28f);
            public static readonly Vector3 GoalOutlineLocalPosition = new Vector3(0f, 1.07f, 0f);
            public static readonly Vector3 GoalOutlineLocalScale = new Vector3(0.36f, 0.02f, 0.36f);
            public const float ProxyScale = 1.002f;

            public static readonly IReadOnlyDictionary<SphereColor, string> MaterialPaths =
                new Dictionary<SphereColor, string>
                {
                    { SphereColor.Aqua, "Assets/ColorPuzzle/Materials/Underwater/Underwater_Aqua.mat" },
                    { SphereColor.DeepBlue, "Assets/ColorPuzzle/Materials/Underwater/Underwater_DeepBlue.mat" },
                    { SphereColor.PurpleRock, "Assets/ColorPuzzle/Materials/Underwater/Underwater_PurpleRock.mat" },
                    { SphereColor.CoralOrange, "Assets/ColorPuzzle/Materials/Underwater/Underwater_CoralOrange.mat" },
                    { SphereColor.CoralPink, "Assets/ColorPuzzle/Materials/Underwater/Underwater_CoralPink.mat" },
                    { SphereColor.KelpGreen, "Assets/ColorPuzzle/Materials/Underwater/Underwater_KelpGreen.mat" },
                    { SphereColor.BubbleCyan, "Assets/ColorPuzzle/Materials/Underwater/Underwater_BubbleCyan.mat" },
                    { SphereColor.TreasureGold, "Assets/ColorPuzzle/Materials/Underwater/Underwater_TreasureGold.mat" }
                };

            public static readonly SphereSpec[] Spheres =
            {
                new SphereSpec("DecoySphere_Blue_Near", new Vector3(-4f, 0.65f, -1.5f), 0.75f, SphereColor.DeepBlue),
                new SphereSpec("DecoySphere_Aqua_Path", new Vector3(-2.5f, 1f, 2.6f), 0.62f, SphereColor.Aqua),
                new SphereSpec("DecoySphere_Gold_Center", new Vector3(0.2f, 0.7f, 3.4f), 0.68f, SphereColor.TreasureGold),
                new SphereSpec("DecoySphere_Pink_Right", new Vector3(4.2f, 0.75f, 2f), 0.82f, SphereColor.CoralPink),
                new SphereSpec("DecoySphere_Purple_Ridge", new Vector3(6.8f, 1.15f, 5f), 0.88f, SphereColor.PurpleRock),
                new SphereSpec("DecoySphere_Orange_Left", new Vector3(-6.5f, 1.85f, 6.3f), 0.92f, SphereColor.CoralOrange),
                new SphereSpec("DecoySphere_Blue_Reef", new Vector3(-1.8f, 2.35f, 6f), 0.72f, SphereColor.DeepBlue),
                new SphereSpec("DecoySphere_Cyan_High", new Vector3(2.8f, 2.85f, 7.4f), 0.58f, SphereColor.BubbleCyan),
                new SphereSpec("DecoySphere_Green_Right", new Vector3(7.5f, 0.8f, -2.8f), 0.78f, SphereColor.KelpGreen),
                new SphereSpec("DecoySphere_Gold_Left", new Vector3(-7.6f, 0.7f, -3.5f), 0.58f, SphereColor.TreasureGold),
                new SphereSpec("DecoySphere_Pink_Floating", new Vector3(-4.5f, 3.8f, 1.8f), 0.66f, SphereColor.CoralPink),
                new SphereSpec("DecoySphere_Orange_Floating", new Vector3(4.5f, 3.5f, 6.8f), 0.62f, SphereColor.CoralOrange),
                new SphereSpec("DecoySphere_Aqua_Arch", new Vector3(8f, 2.2f, 8f), 0.82f, SphereColor.Aqua),
                new SphereSpec("DecoySphere_Purple_Back", new Vector3(-6f, 0.85f, 9.5f), 0.72f, SphereColor.PurpleRock)
            };
        }

        [MenuItem("Color Puzzle/Refine Underwater Demo (New Objects Only)")]
        public static void RefineUnderwaterDemo()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Config.UnderwaterScene)
                throw new InvalidOperationException($"Open {Config.UnderwaterScene} before running this targeted refinement. Nothing was changed.");
            if (FindSceneObject(Config.SphereGroupName, scene) != null)
            {
                Debug.Log("The underwater refinement already exists. Nothing was overwritten.");
                return;
            }

            FirstPersonPlayer player = UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>(FindObjectsInactive.Include);
            GoalPedestal oldGoal = UnityEngine.Object.FindAnyObjectByType<GoalPedestal>(FindObjectsInactive.Include);
            if (player == null || oldGoal == null)
                throw new InvalidOperationException("The underwater player or red goal could not be found. Nothing was changed.");

            Material grayMaterial = LoadRequiredAsset<Material>(Config.WorldGrayMaterial);
            Dictionary<SphereColor, Material> sphereMaterials = LoadSphereMaterials();
            GameObject decoyPrefab = CreateDecoySpherePrefab(grayMaterial, sphereMaterials[SphereColor.Aqua]);
            GameObject goalPrefab = CreateUnderwaterGoalPrefab();

            ApplyGravity(player);
            ReplaceGoal(scene, oldGoal, goalPrefab);
            PopulateDecoys(scene, decoyPrefab, sphereMaterials);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, Config.UnderwaterScene))
                throw new InvalidOperationException("Unity could not save the underwater scene refinement.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Refined the underwater demo with non-interactive colored decoy spheres, lower gravity, and a cylindrical outlined goal. The original level and shared goal prefab were not modified.");
        }

        private static void ApplyGravity(FirstPersonPlayer player)
        {
            SerializedObject serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty("gravity").floatValue = Config.PlayerGravity;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        }

        private static Dictionary<SphereColor, Material> LoadSphereMaterials()
        {
            Dictionary<SphereColor, Material> materials = new Dictionary<SphereColor, Material>();
            foreach (KeyValuePair<SphereColor, string> entry in Config.MaterialPaths)
                materials.Add(entry.Key, LoadRequiredAsset<Material>(entry.Value));
            return materials;
        }

        private static GameObject CreateDecoySpherePrefab(Material grayMaterial, Material defaultColorMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Config.DecoySpherePrefab);
            if (existing != null) return existing;

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SceneManager.MoveGameObjectToScene(root, preview);
                root.name = "UnderwaterDecoySphere";
                root.GetComponent<Renderer>().sharedMaterial = grayMaterial;

                GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SceneManager.MoveGameObjectToScene(proxy, preview);
                proxy.name = "ColorProxy";
                proxy.transform.SetParent(root.transform, false);
                proxy.transform.localScale = Vector3.one * Config.ProxyScale;
                Collider proxyCollider = proxy.GetComponent<Collider>();
                if (proxyCollider != null) UnityEngine.Object.DestroyImmediate(proxyCollider);
                int revealLayer = LayerMask.NameToLayer("ColorReveal");
                if (revealLayer >= 0) proxy.layer = revealLayer;
                proxy.GetComponent<Renderer>().sharedMaterial = defaultColorMaterial;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, Config.DecoySpherePrefab);
                if (saved == null) throw new InvalidOperationException("Unity could not create the underwater decoy-sphere prefab.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            return LoadRequiredAsset<GameObject>(Config.DecoySpherePrefab);
        }

        private static GameObject CreateUnderwaterGoalPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Config.UnderwaterGoalPrefab);
            if (existing != null) return existing;
            if (!AssetDatabase.CopyAsset(Config.SourceGoalPrefab, Config.UnderwaterGoalPrefab))
                throw new InvalidOperationException("Unity could not copy the shared goal into an underwater-only prefab.");

            GameObject root = PrefabUtility.LoadPrefabContents(Config.UnderwaterGoalPrefab);
            try
            {
                Mesh cylinderMesh = GetPrimitiveMesh(PrimitiveType.Cylinder);
                root.name = "UnderwaterCylinderGoal";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Config.GoalScale;
                root.GetComponent<MeshFilter>().sharedMesh = cylinderMesh;

                BoxCollider boxCollider = root.GetComponent<BoxCollider>();
                if (boxCollider != null) UnityEngine.Object.DestroyImmediate(boxCollider);
                MeshCollider meshCollider = root.GetComponent<MeshCollider>();
                if (meshCollider == null) meshCollider = root.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = cylinderMesh;

                Transform colorProxy = FindChild(root.transform, "ColorProxy_Red");
                Transform target = FindChild(root.transform, "RedHintMark");
                Transform socket = FindChild(root.transform, "PlacementSocket");
                if (colorProxy == null || target == null || socket == null)
                    throw new InvalidOperationException("The copied goal prefab is missing an expected child.");

                colorProxy.GetComponent<MeshFilter>().sharedMesh = cylinderMesh;
                target.localPosition = Config.GoalTargetLocalPosition;
                target.localScale = Config.GoalTargetLocalScale;
                socket.localPosition = Config.GoalPlacementSocketLocalPosition;

                GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                SceneManager.MoveGameObjectToScene(outline, root.scene);
                outline.name = "PlacementTargetWhiteOutline";
                outline.transform.SetParent(root.transform, false);
                outline.transform.localPosition = Config.GoalOutlineLocalPosition;
                outline.transform.localScale = Config.GoalOutlineLocalScale;
                Collider outlineCollider = outline.GetComponent<Collider>();
                if (outlineCollider != null) UnityEngine.Object.DestroyImmediate(outlineCollider);
                outline.GetComponent<Renderer>().sharedMaterial = LoadRequiredAsset<Material>(Config.WorldGrayMaterial);

                if (PrefabUtility.SaveAsPrefabAsset(root, Config.UnderwaterGoalPrefab) == null)
                    throw new InvalidOperationException("Unity could not save the underwater cylindrical goal prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return LoadRequiredAsset<GameObject>(Config.UnderwaterGoalPrefab);
        }

        private static void ReplaceGoal(Scene scene, GoalPedestal oldGoal, GameObject goalPrefab)
        {
            Transform oldTransform = oldGoal.transform;
            Transform parent = oldTransform.parent;
            Vector3 localPosition = oldTransform.localPosition;
            Quaternion localRotation = oldTransform.localRotation;
            int siblingIndex = oldTransform.GetSiblingIndex();

            GameObject replacement = PrefabUtility.InstantiatePrefab(goalPrefab, scene) as GameObject;
            if (replacement == null) throw new InvalidOperationException("Unity could not instantiate the underwater goal prefab.");
            replacement.name = "UnderwaterCylinderGoal";
            replacement.transform.SetParent(parent, false);
            replacement.transform.localPosition = localPosition;
            replacement.transform.localRotation = localRotation;
            replacement.transform.localScale = Config.GoalScale;
            replacement.transform.SetSiblingIndex(siblingIndex);

            GoalPedestal newGoal = replacement.GetComponent<GoalPedestal>();
            CopyGoalSettings(oldGoal, newGoal);
            UnityEngine.Object.DestroyImmediate(oldGoal.gameObject);
        }

        private static void CopyGoalSettings(GoalPedestal source, GoalPedestal destination)
        {
            SerializedObject sourceObject = new SerializedObject(source);
            SerializedObject destinationObject = new SerializedObject(destination);
            string[] preservedProperties =
            {
                "acceptedObjectId", "completedMaterial", "createCompletionDoor",
                "completionDoorWorldPosition", "completionDoorWorldEulerAngles", "completionDoorWorldScale",
                "completionDoorBaseColor", "completionDoorRevealColor", "completionDoorName"
            };

            foreach (string propertyName in preservedProperties)
            {
                SerializedProperty sourceProperty = sourceObject.FindProperty(propertyName);
                SerializedProperty destinationProperty = destinationObject.FindProperty(propertyName);
                if (sourceProperty != null && destinationProperty != null)
                    destinationObject.CopyFromSerializedProperty(sourceProperty);
            }
            destinationObject.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(destination);
        }

        private static void PopulateDecoys(Scene scene, GameObject decoyPrefab, IReadOnlyDictionary<SphereColor, Material> materials)
        {
            GameObject additionsRoot = FindSceneObject(Config.AdditionsRootName, scene);
            GameObject group = new GameObject(Config.SphereGroupName);
            SceneManager.MoveGameObjectToScene(group, scene);
            if (additionsRoot != null) group.transform.SetParent(additionsRoot.transform, false);

            foreach (SphereSpec spec in Config.Spheres)
            {
                GameObject sphere = PrefabUtility.InstantiatePrefab(decoyPrefab, scene) as GameObject;
                if (sphere == null) throw new InvalidOperationException($"Unity could not instantiate {spec.Name}.");
                sphere.name = spec.Name;
                sphere.transform.SetParent(group.transform, false);
                sphere.transform.localPosition = spec.Position;
                sphere.transform.localRotation = Quaternion.identity;
                sphere.transform.localScale = Vector3.one * spec.Diameter;

                Transform proxy = FindChild(sphere.transform, "ColorProxy");
                proxy.GetComponent<Renderer>().sharedMaterial = materials[spec.Color];
                PrefabUtility.RecordPrefabInstancePropertyModifications(proxy.GetComponent<Renderer>());
            }
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType primitiveType)
        {
            GameObject temporary = GameObject.CreatePrimitive(primitiveType);
            Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temporary);
            return mesh;
        }

        private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Required asset is missing: {path}");
            return asset;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }

        private static GameObject FindSceneObject(string objectName, Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = FindChild(root.transform, objectName);
                if (match != null) return match.gameObject;
            }
            return null;
        }
    }
}
#endif
