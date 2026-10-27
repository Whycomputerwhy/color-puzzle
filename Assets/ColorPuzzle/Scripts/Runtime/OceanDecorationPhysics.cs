using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorPuzzle
{
    /// <summary>
    /// Makes the collision authored on the decoration prefabs available at runtime.
    /// Decoration placement and collider shape are intentionally owned by the prefab.
    /// </summary>
    internal static class OceanDecorationPhysics
    {
        private const string DecorationsRootName = "--- OCEAN DECORATIONS ---";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnableAuthoredDecorationColliders()
        {
            SceneManager.sceneLoaded += EnableAuthoredDecorationColliders;
            EnableAuthoredDecorationColliders(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void EnableAuthoredDecorationColliders(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform decorations = FindChild(root.transform, DecorationsRootName);
                if (decorations == null) continue;

                // The prefab's collider is the source of truth. Do not create or resize
                // runtime collision geometry here: that would drift from prefab edits.
                foreach (Collider collider in decorations.GetComponentsInChildren<Collider>(true))
                    collider.enabled = true;
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
