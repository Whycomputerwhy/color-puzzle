#if UNITY_EDITOR
using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ColorPuzzle.Editor
{
    [InitializeOnLoad]
    internal static class InteractionOutlineRendererFeatureInstaller
    {
        private const string SessionKey = "ColorPuzzle.InteractionOutlineRendererFeatureInstaller.v2";

        static InteractionOutlineRendererFeatureInstaller()
        {
            EditorApplication.delayCall += EnsureInstalledOnce;
        }

        [MenuItem("Color Puzzle/Rendering/Ensure Interaction Outline Feature")]
        private static void EnsureInstalledFromMenu()
        {
            EnsureInstalled();
        }

        private static void EnsureInstalledOnce()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EnsureInstalled();
        }

        private static void EnsureInstalled()
        {
            string[] rendererDataGuids = AssetDatabase.FindAssets("t:UniversalRendererData");
            bool changed = false;
            foreach (string guid in rendererDataGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                // Never mutate Unity package-cache defaults. Only project-owned assets
                // belong to this game and are safe to extend.
                if (!assetPath.StartsWith("Assets/", System.StringComparison.Ordinal)) continue;
                UniversalRendererData rendererData =
                    AssetDatabase.LoadAssetAtPath<UniversalRendererData>(assetPath);
                if (rendererData == null) continue;

                if (!HasOutlineFeature(rendererData))
                {
                    InteractionOutlineRendererFeature feature =
                        ScriptableObject.CreateInstance<InteractionOutlineRendererFeature>();
                    feature.name = "Interaction Soft Outline";
                    feature.hideFlags = HideFlags.HideInHierarchy;
                    feature.SetActive(true);
                    AssetDatabase.AddObjectToAsset(feature, rendererData);
                    rendererData.rendererFeatures.Add(feature);
                    changed = true;
                    Debug.Log($"Added Interaction Soft Outline to {assetPath}.", rendererData);
                }

                changed |= SynchronizeFeatureMap(rendererData);
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static bool HasOutlineFeature(UniversalRendererData rendererData)
        {
            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature is InteractionOutlineRendererFeature) return true;
            }
            return false;
        }

        private static bool SynchronizeFeatureMap(UniversalRendererData rendererData)
        {
            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
            if (featureMap == null) return false;

            bool changed = featureMap.arraySize != rendererData.rendererFeatures.Count;
            featureMap.arraySize = rendererData.rendererFeatures.Count;
            for (int index = 0; index < rendererData.rendererFeatures.Count; index++)
            {
                long localId = 0;
                ScriptableRendererFeature feature = rendererData.rendererFeatures[index];
                if (feature != null)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out localId);

                SerializedProperty mappedId = featureMap.GetArrayElementAtIndex(index);
                if (mappedId.longValue == localId) continue;
                mappedId.longValue = localId;
                changed = true;
            }

            if (changed) serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }
    }
}
#endif
