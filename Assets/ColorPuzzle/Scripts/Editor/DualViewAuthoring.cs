using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    public static class DualViewAuthoring
    {
        [MenuItem("Color Puzzle/Set Up Selected Objects for Dual View")]
        private static void SetUpSelectedObjects()
        {
            int added = 0;
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(selected))
                {
                    Debug.LogWarning($"Open {selected.name} in Prefab Mode before setting up its child materials.", selected);
                    continue;
                }

                foreach (MeshRenderer renderer in selected.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.gameObject.layer == LayerMask.NameToLayer("ColorReveal") ||
                        renderer.GetComponent<DualViewMaterial>() != null) continue;

                    DualViewMaterial appearance = Undo.AddComponent<DualViewMaterial>(renderer.gameObject);
                    SerializedObject serialized = new SerializedObject(appearance);
                    AssignMaterials(serialized.FindProperty("greyscaleMaterials"), renderer.sharedMaterials);

                    GameObject proxy = FindColorProxy(renderer.transform);
                    Renderer proxyRenderer = proxy != null ? proxy.GetComponent<Renderer>() : null;
                    AssignMaterials(serialized.FindProperty("colorMaterials"),
                        proxyRenderer != null ? proxyRenderer.sharedMaterials : renderer.sharedMaterials);
                    serialized.FindProperty("legacyColorProxy").objectReferenceValue = proxy;
                    serialized.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(appearance);
                    added++;
                }
            }

            Debug.Log($"Set up {added} new dual-view renderer(s). Choose each object's Color Materials in its Inspector; unchanged defaults remain visible in both views.");
        }

        [MenuItem("Color Puzzle/Set Up Selected Objects for Dual View", true)]
        private static bool CanSetUpSelectedObjects() => Selection.gameObjects.Length > 0;

        private static GameObject FindColorProxy(Transform source)
        {
            foreach (Transform child in source)
                if (child.gameObject.layer == LayerMask.NameToLayer("ColorReveal") &&
                    child.GetComponent<Renderer>() != null)
                    return child.gameObject;
            return null;
        }

        private static void AssignMaterials(SerializedProperty array, Material[] materials)
        {
            array.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
        }
    }
}
