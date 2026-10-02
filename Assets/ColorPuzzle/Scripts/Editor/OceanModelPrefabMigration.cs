#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ColorPuzzle.Editor
{
    /// <summary>
    /// Replaces only the underwater goal and pickup visuals. Authored scene transforms and
    /// layout are intentionally left untouched.
    /// </summary>
    public static class OceanModelPrefabMigration
    {
        private readonly struct TintSpec
        {
            public readonly string Name;
            public readonly Color Color;

            public TintSpec(string name, Color color)
            {
                Name = name;
                Color = color;
            }
        }

        // CENTRALIZED OCEAN MODEL PARAMETERS
        private static class Config
        {
            public const string UnderwaterScene = "Assets/ColorPuzzle/Scenes/ColorPuzzleUnderwaterDemo.unity";
            public const string PillarModel = "Assets/ColorPuzzle/Models/Ocean/pillar_ocean.fbx";
            public const string ShellModel = "Assets/ColorPuzzle/Models/Ocean/shell_ocean.fbx";
            public const string PillarTexture = "Assets/ColorPuzzle/Models/Ocean/pillar_texture.png";
            public const string ShellTexture = "Assets/ColorPuzzle/Models/Ocean/shell_texture.png";
            public const string GoalPrefab = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterCylinderGoal.prefab";
            public const string RedBallPrefab = "Assets/ColorPuzzle/Prefabs/Gameplay/DiscoverableRedBall.prefab";
            public const string DecoyBallPrefab = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterDecoySphere.prefab";
            public const string GeneratedFolder = "Assets/ColorPuzzle/Generated/Ocean";
            public const string ShellMeshAsset = GeneratedFolder + "/ShellOcean_Normalized.asset";
            public const string PillarVisualName = "PillarOceanVisual";
            public const string RedObjectId = "red-ball";
            public const string DecoyObjectId = "decoy-ball";

            // Shells replace one-unit sphere meshes, but intentionally occupy less space.
            public const float ShellVisualMaxSize = 0.76f;
            public const float ShellColliderRadius = 0.38f;
            public const float ColorProxyScale = 1.002f;
            public static readonly Vector3 ShellAxisEuler = new Vector3(90f, 0f, 0f);

            // Both models were authored with Z as their thickness/height axis. The pillar
            // wrapper cancels the legacy goal root's (2.2, 0.5, 2.2) non-uniform scale.
            public static readonly Vector3 PillarAxisEuler = new Vector3(90f, 0f, 0f);
            public const float PillarWorldDiameter = 1.6f;
            public const float PillarWorldBottomY = -0.5f;
            public const float PlacementTargetSurfaceOffset = 0.01f;
            public const float PlacementSocketClearance = 0f;

            public static readonly TintSpec[] ShellTints =
            {
                new TintSpec("Gray", new Color(0.62f, 0.62f, 0.62f, 1f)),
                new TintSpec("Red", new Color(0.92f, 0.08f, 0.06f, 1f)),
                new TintSpec("Aqua", new Color(0.12f, 0.62f, 0.58f, 1f)),
                new TintSpec("DeepBlue", new Color(0.08f, 0.25f, 0.58f, 1f)),
                new TintSpec("PurpleRock", new Color(0.34f, 0.19f, 0.52f, 1f)),
                new TintSpec("CoralOrange", new Color(0.96f, 0.34f, 0.15f, 1f)),
                new TintSpec("CoralPink", new Color(0.92f, 0.18f, 0.48f, 1f)),
                new TintSpec("KelpGreen", new Color(0.09f, 0.50f, 0.24f, 1f)),
                new TintSpec("BubbleCyan", new Color(0.38f, 0.90f, 1f, 1f)),
                new TintSpec("TreasureGold", new Color(1f, 0.72f, 0.08f, 1f))
            };
        }

        [MenuItem("Color Puzzle/Apply Ocean Pillar and Shell Models")]
        public static void ApplyOceanModels()
        {
            if (SceneManager.GetActiveScene().path != Config.UnderwaterScene)
                throw new InvalidOperationException($"Open {Config.UnderwaterScene} first. Nothing was changed.");

            EnsureFolder(Config.GeneratedFolder);
            PrepareReadableModel(Config.ShellModel);

            GameObject pillarModel = LoadRequired<GameObject>(Config.PillarModel);
            GameObject shellModel = LoadRequired<GameObject>(Config.ShellModel);
            Texture2D pillarTexture = LoadRequired<Texture2D>(Config.PillarTexture);
            Texture2D shellTexture = LoadRequired<Texture2D>(Config.ShellTexture);
            Mesh shellMesh = BuildNormalizedShellMesh(shellModel);
            Material sourceShellMaterial = FindFirstMaterial(shellModel);
            Dictionary<string, Material> shellMaterials = BuildShellMaterials(sourceShellMaterial, shellTexture);

            UpdatePickupPrefab(Config.RedBallPrefab, shellMesh, shellMaterials["Gray"], shellMaterials["Red"]);
            UpdatePickupPrefab(Config.DecoyBallPrefab, shellMesh, shellMaterials["Gray"], shellMaterials["Aqua"]);
            UpdateGoalPrefab(pillarModel, pillarTexture);
            MigrateSceneShellColors(shellMaterials);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Config.UnderwaterScene);
            AssetDatabase.Refresh();
            Debug.Log("Applied corrected ocean-model axes and textured shell visuals while preserving authored shell orientations.");
        }

        private static void UpdatePickupPrefab(
            string prefabPath,
            Mesh shellMesh,
            Material grayMaterial,
            Material revealMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                MeshFilter rootFilter = root.GetComponent<MeshFilter>();
                MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();
                SphereCollider sphereCollider = root.GetComponent<SphereCollider>();
                DiscoverableObject discoverable = root.GetComponent<DiscoverableObject>();
                if (rootFilter == null || rootRenderer == null || sphereCollider == null || discoverable == null)
                    throw new InvalidOperationException($"{prefabPath} is missing an expected pickup component.");

                rootFilter.sharedMesh = shellMesh;
                rootRenderer.sharedMaterial = grayMaterial;
                sphereCollider.radius = Config.ShellColliderRadius;

                SerializedObject serializedDiscoverable = new SerializedObject(discoverable);
                SerializedProperty proxyProperty = serializedDiscoverable.FindProperty("colorProxy");
                GameObject proxy = proxyProperty.objectReferenceValue as GameObject;
                if (proxy == null) throw new InvalidOperationException($"{prefabPath} has no color proxy.");

                MeshFilter proxyFilter = proxy.GetComponentInChildren<MeshFilter>(true);
                MeshRenderer proxyRenderer = proxy.GetComponentInChildren<MeshRenderer>(true);
                if (proxyFilter == null || proxyRenderer == null)
                    throw new InvalidOperationException($"{prefabPath} has an incomplete color proxy.");

                proxy.transform.localPosition = Vector3.zero;
                proxy.transform.localRotation = Quaternion.identity;
                proxy.transform.localScale = Vector3.one * Config.ColorProxyScale;
                proxyFilter.sharedMesh = shellMesh;
                proxyRenderer.sharedMaterial = revealMaterial;

                SerializedProperty discoveredMaterial = serializedDiscoverable.FindProperty("discoveredMaterial");
                if (discoverable.ObjectId == Config.RedObjectId)
                    discoveredMaterial.objectReferenceValue = revealMaterial;
                serializedDiscoverable.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UpdateGoalPrefab(GameObject pillarModel, Texture pillarTexture)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Config.GoalPrefab);
            try
            {
                Transform existingVisual = FindChild(root.transform, Config.PillarVisualName);
                if (existingVisual != null) UnityEngine.Object.DestroyImmediate(existingVisual.gameObject);

                GameObject visual = new GameObject(Config.PillarVisualName);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = InverseScale(root.transform.localScale);

                GameObject model = PrefabUtility.InstantiatePrefab(pillarModel, visual.transform) as GameObject;
                if (model == null) throw new InvalidOperationException("Could not instantiate the pillar model in the goal prefab.");
                model.name = "PillarOceanModel";
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(Config.PillarAxisEuler));
                model.transform.localScale = Vector3.one;
                SetLayerRecursively(visual, 0);
                float pillarTopWorldY = NormalizePillarVisual(model);
                ConvertPillarMaterials(model, pillarTexture);

                MeshRenderer oldPedestalRenderer = root.GetComponent<MeshRenderer>();
                if (oldPedestalRenderer != null) oldPedestalRenderer.enabled = false;
                Transform oldColorProxy = FindChild(root.transform, "ColorProxy_Red");
                if (oldColorProxy != null) oldColorProxy.gameObject.SetActive(false);

                Transform placementTarget = FindChild(root.transform, "PlacementTargetFlatImage");
                if (placementTarget != null)
                    placementTarget.localPosition = new Vector3(
                        placementTarget.localPosition.x,
                        WorldHeightToGoalLocal(root.transform, pillarTopWorldY + Config.PlacementTargetSurfaceOffset),
                        placementTarget.localPosition.z);

                Transform placementSocket = FindChild(root.transform, "PlacementSocket");
                if (placementSocket != null)
                    placementSocket.localPosition = new Vector3(
                        placementSocket.localPosition.x,
                        WorldHeightToGoalLocal(
                            root.transform,
                            pillarTopWorldY + Config.ShellColliderRadius + Config.PlacementSocketClearance),
                        placementSocket.localPosition.z);

                GoalPedestal goal = root.GetComponent<GoalPedestal>();
                SerializedObject serializedGoal = new SerializedObject(goal);
                serializedGoal.FindProperty("pedestalRenderer").objectReferenceValue = model.GetComponentInChildren<Renderer>(true);
                serializedGoal.FindProperty("completedMaterial").objectReferenceValue = null;
                serializedGoal.FindProperty("colorProxy").objectReferenceValue = null;
                serializedGoal.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, Config.GoalPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void MigrateSceneShellColors(IReadOnlyDictionary<string, Material> shellMaterials)
        {
            DiscoverableObject[] objects = Resources.FindObjectsOfTypeAll<DiscoverableObject>();

            foreach (DiscoverableObject discoverable in objects)
            {
                if (EditorUtility.IsPersistent(discoverable) || discoverable.gameObject.scene != SceneManager.GetActiveScene())
                    continue;
                if (discoverable.ObjectId != Config.DecoyObjectId && discoverable.ObjectId != Config.RedObjectId) continue;
                SerializedObject serialized = new SerializedObject(discoverable);
                GameObject proxy = serialized.FindProperty("colorProxy").objectReferenceValue as GameObject;
                Renderer proxyRenderer = proxy != null ? proxy.GetComponentInChildren<Renderer>(true) : null;
                if (proxyRenderer == null) continue;

                if (discoverable.ObjectId == Config.DecoyObjectId)
                {
                    Color oldColor = ReadMaterialColor(proxyRenderer.sharedMaterial);
                    Material closest = FindClosestTint(oldColor, shellMaterials);
                    proxyRenderer.sharedMaterial = closest;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(proxyRenderer);
                    EditorUtility.SetDirty(proxyRenderer);
                }

                // Preserve authored scene orientation. The mesh's axis correction is already
                // baked into the normalized mesh, so a root yaw can turn flat shells end-on.
                discoverable.transform.localRotation = Quaternion.identity;
                PrefabUtility.RecordPrefabInstancePropertyModifications(discoverable.transform);
                EditorUtility.SetDirty(discoverable.transform);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static Mesh BuildNormalizedShellMesh(GameObject shellModel)
        {
            MeshFilter[] filters = shellModel.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) throw new InvalidOperationException("The shell model contains no mesh filters.");

            Matrix4x4 rootWorldToLocal = shellModel.transform.worldToLocalMatrix;
            List<CombineInstance> combine = new List<CombineInstance>(filters.Length);
            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                combine.Add(new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    transform = rootWorldToLocal * filter.transform.localToWorldMatrix
                });
            }

            Mesh normalized = new Mesh
            {
                name = "ShellOcean_Normalized",
                indexFormat = IndexFormat.UInt32
            };
            normalized.CombineMeshes(combine.ToArray(), true, true, false);
            Quaternion axisCorrection = Quaternion.Euler(Config.ShellAxisEuler);
            Vector3[] vertices = normalized.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = axisCorrection * vertices[i];
            normalized.vertices = vertices;
            Vector3[] normals = normalized.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = axisCorrection * normals[i];
            normalized.normals = normals;
            normalized.RecalculateBounds();
            Bounds sourceBounds = normalized.bounds;
            float longest = Mathf.Max(sourceBounds.size.x, sourceBounds.size.y, sourceBounds.size.z);
            if (longest <= Mathf.Epsilon) throw new InvalidOperationException("The shell model has zero-sized bounds.");

            float scale = Config.ShellVisualMaxSize / longest;
            Vector3 scaledCenter = sourceBounds.center * scale;
            float scaledMinimumY = sourceBounds.min.y * scale;
            Vector3 offset = new Vector3(-scaledCenter.x, -Config.ShellColliderRadius - scaledMinimumY, -scaledCenter.z);
            vertices = normalized.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = vertices[i] * scale + offset;
            normalized.vertices = vertices;
            normalized.RecalculateBounds();
            normalized.RecalculateTangents();

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(Config.ShellMeshAsset);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(normalized, Config.ShellMeshAsset);
                return normalized;
            }

            EditorUtility.CopySerialized(normalized, existing);
            UnityEngine.Object.DestroyImmediate(normalized);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Dictionary<string, Material> BuildShellMaterials(Material source, Texture shellTexture)
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            foreach (TintSpec tint in Config.ShellTints)
            {
                string path = $"{Config.GeneratedFolder}/Shell_{tint.Name}.mat";
                materials.Add(tint.Name, CreateUrpMaterial(path, source, tint.Color, shellTexture));
            }
            return materials;
        }

        private static void ConvertPillarMaterials(GameObject pillarVisual, Texture pillarTexture)
        {
            Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
            Renderer[] renderers = pillarVisual.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] sourceMaterials = renderers[rendererIndex].sharedMaterials;
                Material[] replacement = new Material[sourceMaterials.Length];
                for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
                {
                    Material source = sourceMaterials[materialIndex];
                    if (source == null) continue;
                    if (!converted.TryGetValue(source, out Material material))
                    {
                        string safeName = SanitizeAssetName(source.name);
                        string path = $"{Config.GeneratedFolder}/Pillar_{safeName}_{converted.Count}.mat";
                        material = CreateUrpMaterial(path, source, Color.white, pillarTexture);
                        converted.Add(source, material);
                    }
                    replacement[materialIndex] = material;
                }
                renderers[rendererIndex].sharedMaterials = replacement;
            }
        }

        private static Material CreateUrpMaterial(string path, Material source, Color tint, Texture textureOverride)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No supported Lit shader is available.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            Texture texture = textureOverride != null ? textureOverride : FindBaseTexture(source);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static float NormalizePillarVisual(GameObject visual)
        {
            Bounds bounds = CalculateLocalRendererBounds(visual.transform.parent.gameObject);
            if (bounds.size.x <= Mathf.Epsilon || bounds.size.y <= Mathf.Epsilon || bounds.size.z <= Mathf.Epsilon)
                throw new InvalidOperationException("The pillar model has invalid bounds.");

            float horizontalDiameter = Mathf.Max(bounds.size.x, bounds.size.z);
            float uniformScale = Config.PillarWorldDiameter / horizontalDiameter;
            visual.transform.localScale = Vector3.one * uniformScale;
            visual.transform.localPosition = new Vector3(
                -bounds.center.x * uniformScale,
                Config.PillarWorldBottomY - bounds.min.y * uniformScale,
                -bounds.center.z * uniformScale);
            return Config.PillarWorldBottomY + bounds.size.y * uniformScale;
        }

        private static Bounds CalculateLocalRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"{root.name} has no renderers.");
            Matrix4x4 worldToLocal = root.transform.worldToLocalMatrix;
            bool initialized = false;
            Bounds result = default;
            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = world.center + Vector3.Scale(world.extents, new Vector3(x, y, z));
                    Vector3 localCorner = worldToLocal.MultiplyPoint3x4(corner);
                    if (!initialized)
                    {
                        result = new Bounds(localCorner, Vector3.zero);
                        initialized = true;
                    }
                    else result.Encapsulate(localCorner);
                }
            }
            return result;
        }

        private static Material FindClosestTint(Color color, IReadOnlyDictionary<string, Material> materials)
        {
            Material best = materials["Aqua"];
            float bestDistance = float.PositiveInfinity;
            foreach (TintSpec tint in Config.ShellTints)
            {
                if (tint.Name == "Gray" || tint.Name == "Red") continue;
                Vector3 difference = new Vector3(color.r - tint.Color.r, color.g - tint.Color.g, color.b - tint.Color.b);
                float distance = difference.sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = materials[tint.Name];
            }
            return best;
        }

        private static Color ReadMaterialColor(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.white;
        }

        private static Texture FindBaseTexture(Material material)
        {
            if (material == null) return null;
            string[] names = { "_BaseMap", "_MainTex", "baseColorTexture" };
            foreach (string name in names)
                if (material.HasProperty(name) && material.GetTexture(name) != null)
                    return material.GetTexture(name);
            return null;
        }

        private static Material FindFirstMaterial(GameObject model)
        {
            Renderer renderer = model.GetComponentInChildren<Renderer>(true);
            if (renderer == null || renderer.sharedMaterial == null)
                throw new InvalidOperationException($"{model.name} has no source material.");
            return renderer.sharedMaterial;
        }

        private static void PrepareReadableModel(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException($"Unity has not imported {path} as a model.");
            if (importer.isReadable) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static Vector3 InverseScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Abs(scale.x) > Mathf.Epsilon ? 1f / scale.x : 1f,
                Mathf.Abs(scale.y) > Mathf.Epsilon ? 1f / scale.y : 1f,
                Mathf.Abs(scale.z) > Mathf.Epsilon ? 1f / scale.z : 1f);
        }

        private static float WorldHeightToGoalLocal(Transform goalRoot, float worldHeightFromGoalOrigin)
        {
            float scaleY = Mathf.Abs(goalRoot.localScale.y) > Mathf.Epsilon ? goalRoot.localScale.y : 1f;
            return worldHeightFromGoalOrigin / scaleY;
        }

        private static float StableSignedRange(string key, uint seed)
        {
            uint hash = 2166136261u ^ seed;
            for (int i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619u;
            }
            return (hash / (float)uint.MaxValue) * 2f - 1f;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = $"{transform.name}[{transform.GetSiblingIndex()}]";
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = $"{parent.name}[{parent.GetSiblingIndex()}]/{path}";
                parent = parent.parent;
            }
            return path;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static string SanitizeAssetName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "Material" : value;
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Required asset not found: {path}");
            return asset;
        }
    }
}
#endif
