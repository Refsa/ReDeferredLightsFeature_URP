using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class CopyColorPass : ScriptableRenderPass
{
    Settings _settings;

    RenderTargetIdentifier colorTarget;
    RenderTargetHandle colorTempHandle;
    RenderTargetHandle depthTempHandle;


    public CopyColorPass(Settings settings)
    {
        _settings = settings;
        renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        colorTempHandle.Init("_CopyColorTemp");
        depthTempHandle.Init("_CopyDepthHandle");
    }

    public void Setup(RenderTargetIdentifier colorTarget)
    {
        this.colorTarget = colorTarget;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        cmd.GetTemporaryRT(colorTempHandle.id, cameraTextureDescriptor);

        var depthDescriptor = new RenderTextureDescriptor(cameraTextureDescriptor.width, cameraTextureDescriptor.height);
        depthDescriptor.depthBufferBits = 32;
        depthDescriptor.msaaSamples = 1;
        depthDescriptor.colorFormat = RenderTextureFormat.Depth;

        cmd.GetTemporaryRT(depthTempHandle.id, depthDescriptor, FilterMode.Point);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("CopyColorPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        ref CameraData cameraData = ref renderingData.cameraData;

        cmd.Blit(colorAttachment, colorTempHandle.Identifier());
        cmd.SetGlobalTexture("_GrabTexture_AfterOpaques", colorTempHandle.Identifier());

        cmd.Blit(depthAttachment, depthTempHandle.Identifier());
        cmd.SetGlobalTexture("_DepthTexture_AfterOpaques", depthTempHandle.Identifier());

        // RenderTargetIdentifier cameraTarget = (cameraData.targetTexture != null) ? new RenderTargetIdentifier(cameraData.targetTexture) : colorTarget;
        // cmd.Blit(colorAttachment, cameraTarget);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(colorTempHandle.id);
        cmd.ReleaseTemporaryRT(depthTempHandle.id);
    }
}