using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorPuzzle
{
    /// <summary>
    /// Ocean decorations are visual dressing only. Imported models occasionally
    /// include oversized colliders, so enforce a collider-free decoration root
    /// when a gameplay scene loads without affecting puzzle or world geometry.
    /// </summary>
    internal static class OceanDecorationPhysics
    {
        private const string DecorationsRootName = "--- OCEAN DECORATIONS ---";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RemoveDecorationColliders()
        {
            SceneManager.sceneLoaded += RemoveDecorationColliders;
            RemoveDecorationColliders(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void RemoveDecorationColliders(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform decorations = FindChild(root.transform, DecorationsRootName);
                if (decorations == null) continue;

                foreach (Collider collider in decorations.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            }
        }

        private static Transform FindChild(Transform current, string name)
        {
            if (current.name == name) return current;
            foreach (Transform child in current)
            {
                Transform found = FindChild(child, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
