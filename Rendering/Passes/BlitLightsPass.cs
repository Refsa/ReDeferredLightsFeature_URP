using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class BlitLightsPass : ScriptableRenderPass
{
    Settings _settings;

    RenderTargetHandle blitHandle;
    RenderTargetIdentifier colorTarget;

    RenderTargetHandle tempHandle;

    Material blitLightsMaterial;

    public BlitLightsPass(Settings settings)
    {
        _settings = settings;
        renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        tempHandle.Init("_BlitLightTemp");
    }

    public void Setup(RenderTargetIdentifier colorTarget, RenderTargetHandle blitHandle, Material blitLightsMaterial)
    {
        this.colorTarget = colorTarget;
        this.blitHandle = blitHandle;
        this.blitLightsMaterial = blitLightsMaterial;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        cmd.GetTemporaryRT(tempHandle.id, cameraTextureDescriptor);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("BlitLightsPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        ref CameraData cameraData = ref renderingData.cameraData;

        if (_settings.DeferredPassOn)
        {
            RenderTargetIdentifier cameraTarget = colorTarget;

            if (cameraData.isSceneViewCamera)
            {
                if (_settings.ShowInSceneView)
                {
                    cmd.Blit(colorAttachment, tempHandle.Identifier(), blitLightsMaterial);
                    cmd.Blit(tempHandle.Identifier(), colorTarget);
                }
            }
            else if (cameraData.isDefaultViewport || cameraData.isStereoEnabled)
            {
                cmd.Blit(colorAttachment, tempHandle.Identifier(), blitLightsMaterial);
                cmd.Blit(tempHandle.Identifier(), colorTarget);
            }
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(tempHandle.id);
    }
}