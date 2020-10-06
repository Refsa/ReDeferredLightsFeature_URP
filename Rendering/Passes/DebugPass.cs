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

    RenderTargetIdentifier backbufferHandle;

    public DebugPass(Settings settings, Material debugMaterial)
    {
        _settings = settings;
        _debugMaterial = debugMaterial;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        backbufferHandle = colorAttachment;
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

        if (cameraData.isDefaultViewport)
        {
            cmd.SetRenderTarget(
                BuiltinRenderTextureType.CurrentActive,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
            cmd.Blit(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive, debugMaterial);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {

    }
}
