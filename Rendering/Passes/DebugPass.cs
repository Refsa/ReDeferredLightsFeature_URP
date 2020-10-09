using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;
using LightData = DeferredLightsFeature.LightData;

class DebugPass : ScriptableRenderPass
{
    const string BackBufferImageID = "_BackBuffer_Image";

    Settings _settings;
    Material _debugMaterial;

    public DebugPass(Settings settings, Material debugMaterial)
    {
        _settings = settings;
        _debugMaterial = debugMaterial;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        renderPassEvent = RenderPassEvent.AfterRendering;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get("_DeferredDebugPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        cmd.SetGlobalTexture(BackBufferImageID, BuiltinRenderTextureType.CurrentActive);
        cmd.SetGlobalInt("_DebugMode", (int)_settings.DebugMode);

        ref CameraData cameraData = ref renderingData.cameraData;

        Material debugMaterial = cameraData.isSceneViewCamera ? null : _debugMaterial;
        debugMaterial?.SetMatrix("MATRIX_IV", cameraData.camera.cameraToWorldMatrix);

        RenderTargetIdentifier cameraTarget = (cameraData.targetTexture != null) ? new RenderTargetIdentifier(cameraData.targetTexture) : BuiltinRenderTextureType.CurrentActive;

        if (cameraData.isDefaultViewport || cameraData.isSceneViewCamera || cameraData.isStereoEnabled)
        {
            cmd.SetRenderTarget(
                cameraTarget,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
            cmd.Blit(cameraTarget, cameraTarget, debugMaterial);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {

    }
}
