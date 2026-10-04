using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorPuzzle
{
    /// <summary>
    /// During the colour camera render, hides a shell's grey source mesh when that shell has
    /// an active colour proxy. The proxy can then use its regular URP/Lit material: it receives
    /// lighting and is depth-tested against the world, so it cannot show through geometry.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ColorRevealShellCameraFilter : MonoBehaviour
    {
        private readonly List<Renderer> temporarilyHidden = new();
        private Camera targetCamera;

        private void Awake() => targetCamera = GetComponent<Camera>();

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RestoreGreyShells();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != targetCamera) return;

            RestoreGreyShells();
            foreach (DiscoverableObject shell in DiscoverableObject.ActiveDiscoverableObjects)
            {
                if (shell == null || !shell.HasActiveColorProxy) continue;
                Renderer greyRenderer = shell.GreyscaleRenderer;
                if (greyRenderer == null || !greyRenderer.enabled) continue;

                greyRenderer.enabled = false;
                temporarilyHidden.Add(greyRenderer);
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == targetCamera) RestoreGreyShells();
        }

        private void RestoreGreyShells()
        {
            foreach (Renderer greyRenderer in temporarilyHidden)
                if (greyRenderer != null) greyRenderer.enabled = true;
            temporarilyHidden.Clear();
        }
    }
}
