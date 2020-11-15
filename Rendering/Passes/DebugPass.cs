using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class DebugPass : ScriptableRenderPass
{
    const string BackBufferImageID = "_BackBuffer_Image";

    Settings _settings;
    Material _debugMaterial;

    RenderTargetIdentifier colorTarget;

    static Texture2D heatmapTexture;

    public DebugPass(Settings settings, Material debugMaterial)
    {
        _settings = settings;
        _debugMaterial = debugMaterial;

        renderPassEvent = RenderPassEvent.AfterRendering;

        if (heatmapTexture is null)
            heatmapTexture = Resources.Load<Texture2D>("Textures/tdr_heatmap") as Texture2D;
    }

    public void Setup(RenderTargetIdentifier colorTarget)
    {
        this.colorTarget = colorTarget;
    }

    public void SetMaterial(Material debugMaterial)
    {
        _debugMaterial = debugMaterial;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get("_DeferredDebugPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        cmd.SetGlobalTexture(BackBufferImageID, colorTarget);

#if UNITY_EDITOR
        cmd.SetGlobalInt("_DebugMode", (int)_settings.DebugMode);
#endif

        cmd.SetGlobalTexture("_HeatmapTexture", heatmapTexture);
        ref CameraData cameraData = ref renderingData.cameraData;

        // Material debugMaterial = cameraData.isSceneViewCamera ? null : _debugMaterial;
        Material debugMaterial = _debugMaterial;
        debugMaterial.SetMatrix("MATRIX_IV", cameraData.camera.cameraToWorldMatrix);

        if (cameraData.isDefaultViewport || cameraData.isSceneViewCamera || cameraData.isStereoEnabled)
        {
            cmd.SetRenderTarget(
                colorTarget,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);

            cmd.Blit(null, colorTarget, debugMaterial);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {

    }
}
