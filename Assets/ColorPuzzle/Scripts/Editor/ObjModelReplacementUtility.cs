using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    /// <summary>
    /// One-shot asset migration for the artist-exported OBJ models. It normalizes OBJ axes
    /// (Y forward, Z up) into the project's existing Unity mesh bounds, so all authored
    /// prefab and scene transforms continue to mean exactly the same thing.
    /// </summary>
    internal static class ObjModelReplacementUtility
    {
        private const string ShellObjPath = "Assets/ColorPuzzle/Models/Ocean/OBJ/shell.obj";
        private const string PillarObjPath = "Assets/ColorPuzzle/Models/Ocean/OBJ/pillar.obj";
        private const string ShellReferencePath = "Assets/ColorPuzzle/Generated/Ocean/ShellOcean_Normalized.asset";
        private const string PillarReferencePath = "Assets/ColorPuzzle/Models/Ocean/pillar_ocean.fbx";
        private const string ShellOutputPath = "Assets/ColorPuzzle/Generated/Ocean/ShellOBJ_Normalized.asset";
        private const string PillarOutputPath = "Assets/ColorPuzzle/Generated/Ocean/PillarOBJ_Normalized.asset";
        private const string RedShellPrefabPath = "Assets/ColorPuzzle/Prefabs/Gameplay/DiscoverableRedBall.prefab";
        private const string DecoyShellPrefabPath = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterDecoySphere.prefab";
        private const string GoalPrefabPath = "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterCylinderGoal.prefab";

        [InitializeOnLoadMethod]
        private static void RunPendingMigrationAfterImport()
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(ShellOutputPath) != null ||
                AssetDatabase.LoadAssetAtPath<Mesh>(PillarOutputPath) != null ||
                AssetDatabase.LoadAssetAtPath<Object>(ShellObjPath) == null ||
                AssetDatabase.LoadAssetAtPath<Object>(PillarObjPath) == null)
                return;

            // Delay until Unity has completed importing the just-added OBJ files.
            EditorApplication.delayCall += ReplaceOceanModelsWithObj;
        }

        [MenuItem("Color Puzzle/Models/Replace Ocean Models With OBJ _F12")]
        public static void ReplaceOceanModelsWithObj()
        {
            Mesh shell = CreateNormalizedMesh(ShellObjPath, ShellReferencePath, ShellOutputPath, "ShellOBJ_Normalized");
            Mesh pillar = CreateNormalizedMesh(PillarObjPath, PillarReferencePath, PillarOutputPath, "PillarOBJ_Normalized");
            ReplaceShellMesh(RedShellPrefabPath, shell);
            ReplaceShellMesh(DecoyShellPrefabPath, shell);
            ReplacePillarMesh(pillar);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Color Puzzle: Replaced ocean shell and pillar visuals with normalized OBJ meshes.");
        }

        private static Mesh CreateNormalizedMesh(string objPath, string referencePath, string outputPath, string assetName)
        {
            Mesh source = LoadFirstMesh(objPath);
            Mesh reference = LoadFirstMesh(referencePath);
            if (source == null || reference == null)
                throw new System.InvalidOperationException($"Could not load OBJ/reference mesh: {objPath} / {referencePath}");

            if (AssetDatabase.LoadAssetAtPath<Mesh>(outputPath) != null)
                AssetDatabase.DeleteAsset(outputPath);

            // OBJ source uses +Y forward and +Z up. Rotate X -90° so +Z becomes Unity +Y.
            Quaternion axisCorrection = Quaternion.Euler(-90f, 0f, 0f);
            Vector3[] rotated = source.vertices.Select(vertex => axisCorrection * vertex).ToArray();
            Bounds sourceBounds = CalculateBounds(rotated);
            Bounds targetBounds = reference.bounds;
            Vector3 scale = new Vector3(
                SafeRatio(targetBounds.size.x, sourceBounds.size.x),
                SafeRatio(targetBounds.size.y, sourceBounds.size.y),
                SafeRatio(targetBounds.size.z, sourceBounds.size.z));
            Vector3[] vertices = rotated.Select(vertex =>
                targetBounds.center + Vector3.Scale(vertex - sourceBounds.center, scale)).ToArray();
            Vector3[] normals = source.normals.Length == source.vertexCount
                ? source.normals.Select(normal => axisCorrection * normal).ToArray()
                : new Vector3[0];

            Mesh result = new Mesh { name = assetName };
            result.indexFormat = source.indexFormat;
            result.vertices = vertices;
            result.uv = source.uv;
            result.uv2 = source.uv2;
            result.tangents = source.tangents;
            result.normals = normals;
            result.subMeshCount = source.subMeshCount;
            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
                result.SetTriangles(source.GetTriangles(subMesh), subMesh);
            if (normals.Length != result.vertexCount) result.RecalculateNormals();
            result.RecalculateBounds();
            AssetDatabase.CreateAsset(result, outputPath);
            return result;
        }

        private static void ReplaceShellMesh(string prefabPath, Mesh mesh)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                    filter.sharedMesh = mesh;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ReplacePillarMesh(Mesh mesh)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GoalPrefabPath);
            try
            {
                Transform visualRoot = root.transform.Find("PillarOceanVisual");
                Renderer oldRenderer = visualRoot != null ? visualRoot.GetComponentInChildren<Renderer>(true) : null;
                if (visualRoot == null || oldRenderer == null)
                    throw new System.InvalidOperationException("Could not locate PillarOceanVisual in the goal prefab.");

                GameObject objVisual = new GameObject("PillarOceanObjModel", typeof(MeshFilter), typeof(MeshRenderer));
                Transform replacement = objVisual.transform;
                replacement.SetParent(oldRenderer.transform.parent, false);
                replacement.localPosition = oldRenderer.transform.localPosition;
                replacement.localRotation = oldRenderer.transform.localRotation;
                replacement.localScale = oldRenderer.transform.localScale;
                objVisual.GetComponent<MeshFilter>().sharedMesh = mesh;
                objVisual.GetComponent<MeshRenderer>().sharedMaterials = oldRenderer.sharedMaterials;
                oldRenderer.gameObject.SetActive(false);

                SerializedObject goal = new SerializedObject(root.GetComponent<GoalPedestal>());
                goal.FindProperty("pedestalRenderer").objectReferenceValue = objVisual.GetComponent<MeshRenderer>();
                goal.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, GoalPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Mesh LoadFirstMesh(string assetPath) =>
            AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Mesh>().FirstOrDefault();

        private static Bounds CalculateBounds(Vector3[] vertices)
        {
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(vertices[i]);
            return bounds;
        }

        private static float SafeRatio(float target, float source) => source > Mathf.Epsilon ? target / source : 1f;
    }
}
