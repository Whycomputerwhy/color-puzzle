using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ColorPuzzle.Rendering
{
    /// <summary>
    /// Composites the colour-camera render through a true world-space sphere.
    /// Unlike a Canvas overlay, this pass runs inside the game camera render and
    /// therefore has a reliable scene depth texture in editor and player builds.
    /// </summary>
    public sealed class WorldSphereRevealRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;

        private Material compositeMaterial;
        private WorldSphereRevealPass revealPass;

        public override void Create()
        {
            name = "World Sphere Color Reveal";
            Shader shader = Resources.Load<Shader>("Shaders/WorldSphereRevealComposite");
            if (shader == null)
            {
                Debug.LogWarning("Missing WorldSphereRevealComposite shader in Resources/Shaders.");
                return;
            }

            compositeMaterial = CoreUtils.CreateEngineMaterial(shader);
            compositeMaterial.name = "World Sphere Reveal Composite";
            revealPass = new WorldSphereRevealPass
            {
                renderPassEvent = renderEvent,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (revealPass == null || compositeMaterial == null || !WorldSphereRevealRuntime.IsActive)
                return;

            if (renderingData.cameraData.camera != WorldSphereRevealRuntime.SourceCamera)
                return;

            revealPass.renderPassEvent = renderEvent;
            revealPass.Material = compositeMaterial;
            renderer.EnqueuePass(revealPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(compositeMaterial);
            compositeMaterial = null;
        }

        private sealed class WorldSphereRevealPass : ScriptableRenderPass
        {
            private static readonly int OriginAndRadiusId = Shader.PropertyToID("_RevealOriginRadius");
            private static readonly int EdgeFeatherId = Shader.PropertyToID("_RevealEdgeFeather");

            public Material Material { private get; set; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (!WorldSphereRevealRuntime.IsActive ||
                    cameraData.camera != WorldSphereRevealRuntime.SourceCamera ||
                    resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle screenColor = resourceData.activeColorTexture;
                TextureHandle screenDepth = resourceData.activeDepthTexture;
                if (!screenColor.IsValid() || !screenDepth.IsValid()) return;

                Material.SetVector(OriginAndRadiusId, WorldSphereRevealRuntime.OriginAndRadius);
                Material.SetFloat(EdgeFeatherId, WorldSphereRevealRuntime.EdgeFeather);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "World Sphere Color Reveal", out PassData passData))
                {
                    passData.Material = Material;
                    builder.SetRenderAttachment(screenColor, 0);
                    // The shader reconstructs the actual visible world position from
                    // this depth texture; keeping it read/write works on URP's depth
                    // attachment variants while preserving the already-rendered scene.
                    builder.SetRenderAttachmentDepth(screenDepth, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        context.cmd.DrawProcedural(
                            Matrix4x4.identity,
                            data.Material,
                            0,
                            MeshTopology.Triangles,
                            3,
                            1));
                }
            }

            private sealed class PassData
            {
                internal Material Material;
            }
        }
    }
}
