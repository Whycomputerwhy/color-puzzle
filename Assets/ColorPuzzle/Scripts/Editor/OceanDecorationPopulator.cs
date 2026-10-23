using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorPuzzle.Editor
{
    /// <summary>
    /// A deliberately small, repeatable dressing pass for the shared ocean room.
    /// It only owns the child group named below, never the hand-placed decorations
    /// that sit directly beneath the scene's ocean-decoration root.
    /// </summary>
    internal static class OceanDecorationPopulator
    {
        private const string DecorationsRootName = "--- OCEAN DECORATIONS ---";
        private const string GeneratedGroupName = "--- GENERATED DECORATION PASS ---";
        private const float FloorY = 603.53f;

        [MenuItem("Color Puzzle/Populate Ocean Decorations")]
        private static void Populate()
        {
            Transform decorationsRoot = FindTransform(DecorationsRootName);
            if (decorationsRoot == null)
            {
                Debug.LogError("Could not find the ocean decorations root in the open scene.");
                return;
            }

            Transform oldPass = decorationsRoot.Find(GeneratedGroupName);
            if (oldPass != null)
                Object.DestroyImmediate(oldPass.gameObject);

            GameObject pass = new GameObject(GeneratedGroupName);
            Undo.RegisterCreatedObjectUndo(pass, "Populate ocean decorations");
            pass.transform.SetParent(decorationsRoot, false);

            GameObject coral = Load("UnderwaterCoral");
            GameObject coralOne = Load("UnderwaterCoral1");
            GameObject coralTwo = Load("UnderwaterCoral2");
            GameObject kelp = Load("UnderwaterKelp");
            GameObject rock = Load("UnderwaterRock");
            GameObject bubbles = Load("UnderwaterBubbles");

            // Keep the central routes readable. The larger corals form visual anchors
            // at the room edges; smaller kelp, rocks, and bubbles soften the gaps.
            Add(coral, pass.transform, "Large Coral A", 67.0f, 264.3f, 25f, 3.6f);
            // Coral1 and Coral2 are imported assets whose model child is already
            // scaled up in the prefab. Keep their roots close to authored size.
            Add(coralOne, pass.transform, "Large Coral B", 103.4f, 264.8f, -30f, 1.25f);
            Add(coralTwo, pass.transform, "Large Coral C", 66.5f, 286.0f, 55f, 1.35f);
            Add(coral, pass.transform, "Large Coral D", 103.8f, 287.0f, -45f, 3.5f);
            Add(coralOne, pass.transform, "Large Coral E", 67.2f, 306.6f, 15f, 1.2f);
            Add(coralTwo, pass.transform, "Large Coral F", 103.0f, 305.5f, 210f, 1.3f);

            Add(kelp, pass.transform, "Kelp A", 70.5f, 268.5f, 150f, 1.9f);
            Add(kelp, pass.transform, "Kelp B", 99.5f, 273.0f, -115f, 2.1f);
            Add(kelp, pass.transform, "Kelp C", 69.5f, 294.8f, 110f, 2.0f);
            Add(kelp, pass.transform, "Kelp D", 100.8f, 299.0f, -125f, 2.2f);
            Add(kelp, pass.transform, "Kelp E", 83.0f, 306.0f, 180f, 1.8f);

            Add(rock, pass.transform, "Rock A", 72.0f, 263.8f, 70f, 1.7f);
            Add(rock, pass.transform, "Rock B", 97.3f, 264.3f, -40f, 1.4f);
            Add(rock, pass.transform, "Rock C", 69.8f, 280.0f, 150f, 2.0f);
            Add(rock, pass.transform, "Rock D", 100.5f, 288.0f, 220f, 1.8f);
            Add(rock, pass.transform, "Rock E", 73.0f, 306.5f, -35f, 1.6f);
            Add(rock, pass.transform, "Rock F", 96.5f, 305.5f, 90f, 1.9f);

            Add(bubbles, pass.transform, "Bubbles A", 73.0f, 271.0f, 0f, 1.6f);
            Add(bubbles, pass.transform, "Bubbles B", 96.0f, 282.5f, 0f, 1.4f);
            Add(bubbles, pass.transform, "Bubbles C", 72.0f, 300.0f, 0f, 1.7f);
            Add(bubbles, pass.transform, "Bubbles D", 97.5f, 301.5f, 0f, 1.5f);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = pass;
            Debug.Log("Added the generated ocean decoration pass. Run the same menu command to replace only this pass.");
        }

        private static GameObject Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/ColorPuzzle/Prefabs/Gameplay/Decorations/{name}.prefab");
        }

        private static void Add(GameObject prefab, Transform parent, string name, float x, float z, float yaw, float scale)
        {
            if (prefab == null)
            {
                Debug.LogError($"Missing decoration prefab for {name}.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.SetPositionAndRotation(
                new Vector3(x, FloorY, z),
                Quaternion.Euler(0f, yaw, 0f));
            instance.transform.localScale = Vector3.one * scale;

            // These are set dressing, not level geometry. Leaving imported
            // colliders active would create invisible obstacles in the room.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        private static Transform FindTransform(string name)
        {
            foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == name)
                    return transform;
            }

            return null;
        }
    }
}
