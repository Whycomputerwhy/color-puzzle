using System;
using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    public static class DualViewValidation
    {
        [MenuItem("Color Puzzle/Validate Campaign Dual Views")]
        public static void ValidateBatch()
        {
            if (LayerMask.NameToLayer("DualView") < 0)
                throw new InvalidOperationException("DualView layer is missing.");

            ValidatePrefab("Assets/ColorPuzzle/Prefabs/Gameplay/DiscoverableRedBall.prefab", null);
            ValidatePrefab("Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterDecoySphere.prefab", null);
            ValidatePrefab("Assets/ColorPuzzle/Prefabs/World/OceanSharedRoom.prefab", "Ocean Floor");
            ValidateCampaignPrefab("Assets/ColorPuzzle/Prefabs/World/OceanSharedRoom.prefab");
            ValidateCampaignPrefab("Assets/ColorPuzzle/Prefabs/World/OceanBackdropRig.prefab");
            ValidateCampaignPrefab("Assets/ColorPuzzle/Prefabs/Level/CampaignStages/00 Magnifier Intro.prefab");
            for (int stage = 1; stage <= 4; stage++)
                ValidateCampaignPrefab($"Assets/ColorPuzzle/Prefabs/Level/CampaignStages/OceanStage{stage:00}.prefab");
            Debug.Log("All four campaign stages, shared world visuals, and legacy shell dual views validated.");
        }

        private static void ValidateCampaignPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<DualViewRoot>() == null)
                throw new InvalidOperationException($"Campaign root is missing DualViewRoot: {path}");

            int colorProxyLayer = LayerMask.NameToLayer("ColorReveal");
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.layer == colorProxyLayer) continue;
                DualViewMaterial appearance = renderer.GetComponent<DualViewMaterial>();
                if (appearance == null)
                    throw new InvalidOperationException($"Authored renderer lacks DualViewMaterial: {path}/{renderer.name}");
                SerializedObject serialized = new SerializedObject(appearance);
                SerializedProperty colors = serialized.FindProperty("colorMaterials");
                SerializedProperty proxy = serialized.FindProperty("legacyColorProxy");
                if ((colors == null || colors.arraySize == 0) &&
                    (proxy == null || proxy.objectReferenceValue == null))
                    throw new InvalidOperationException($"No authored color material or proxy: {path}/{renderer.name}");
            }
        }

        private static void ValidatePrefab(string path, string childName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException($"Missing prefab: {path}");
            Transform target = childName == null ? prefab.transform : FindChild(prefab.transform, childName);
            if (target == null) throw new InvalidOperationException($"Missing {childName} in {path}");
            if (target.GetComponent<DualViewMaterial>() == null)
                throw new InvalidOperationException($"DualViewMaterial missing on {target.name}");

            MeshFilter sourceMesh = target.GetComponent<MeshFilter>();
            Renderer sourceRenderer = target.GetComponent<Renderer>();
            Transform proxy = null;
            foreach (Transform child in target)
                if (child.name.StartsWith("ColorProxy", StringComparison.Ordinal))
                {
                    proxy = child;
                    break;
                }
            MeshFilter proxyMesh = proxy != null ? proxy.GetComponent<MeshFilter>() : null;
            Renderer proxyRenderer = proxy != null ? proxy.GetComponent<Renderer>() : null;
            if (sourceMesh == null || sourceMesh.sharedMesh == null)
                throw new InvalidOperationException($"The shared gameplay mesh is missing: {path}");
            if (sourceRenderer == null || proxyRenderer == null ||
                sourceRenderer.sharedMaterials.Length == 0 || proxyRenderer.sharedMaterials.Length == 0)
                throw new InvalidOperationException($"Missing material set: {path}; sourceRenderer={sourceRenderer}, sourceSlots={sourceRenderer?.sharedMaterials.Length}, proxyRenderer={proxyRenderer}, proxySlots={proxyRenderer?.sharedMaterials.Length}");
            if (proxyMesh == null || proxyMesh.sharedMesh == null)
                Debug.LogWarning($"Legacy ColorProxy mesh does not resolve on {path}; dual-view uses the intact gameplay mesh.");
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}
