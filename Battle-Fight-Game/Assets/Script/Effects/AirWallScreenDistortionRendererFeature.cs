using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AirWallScreenDistortionRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material distortionMaterial;
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    private AirWallScreenDistortionPass distortionPass;

    public override void Create()
    {
        distortionPass = new AirWallScreenDistortionPass
        {
            renderPassEvent = injectionPoint
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (distortionMaterial == null ||
            renderingData.cameraData.cameraType == CameraType.Preview ||
            renderingData.cameraData.cameraType == CameraType.Reflection)
        {
            return;
        }

        distortionPass.renderPassEvent = injectionPoint;
        distortionPass.Setup(distortionMaterial);
        renderer.EnqueuePass(distortionPass);
    }

    protected override void Dispose(bool disposing)
    {
        distortionPass?.Dispose();
    }

    private sealed class AirWallScreenDistortionPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler Profiling = new ProfilingSampler("Air Wall Screen Distortion");
        private Material material;
        private RTHandle colorCopy;

        public void Setup(Material distortionMaterial)
        {
            material = distortionMaterial;
        }

        public void Dispose()
        {
            colorCopy?.Release();
            colorCopy = null;
        }

        [System.Obsolete]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            descriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref colorCopy,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_AirWallScreenColorCopy");
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || colorCopy == null)
            {
                return;
            }

            AirWallScreenDistortion.UpdateGlobals(renderingData.cameraData.camera);

            CommandBuffer cmd = CommandBufferPool.Get("Air Wall Screen Distortion");
            RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

            try
            {
                using (new ProfilingScope(cmd, Profiling))
                {
                    Blitter.BlitCameraTexture(cmd, cameraColor, colorCopy);
                    Blitter.BlitCameraTexture(
                        cmd,
                        colorCopy,
                        cameraColor,
                        RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store,
                        material,
                        0);
                }

                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
