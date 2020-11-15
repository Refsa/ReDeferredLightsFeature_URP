using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class SceneViewBlitPass : ScriptableRenderPass
{
    Settings _settings;
    Material _blitLightsMaterial;

    RenderTargetHandle targetHandle;
    RenderTargetIdentifier colorTarget;

    public SceneViewBlitPass(Settings settings)
    {
        _settings = settings;
        renderPassEvent = RenderPassEvent.AfterRendering;

        targetHandle.Init("_BlitPass");
    }

    public void SetMaterial(Material blitLightsMaterial)
    {
        _blitLightsMaterial = blitLightsMaterial;
    }

    public void Setup(RenderTargetIdentifier colorTarget)
    {
        this.colorTarget = colorTarget;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        cmd.GetTemporaryRT(targetHandle.id, cameraTextureDescriptor);

        // ConfigureTarget(targetHandle.Identifier());
        // ConfigureClear(ClearFlag.None, Color.clear);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("SceneViewBlitPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        ref CameraData cameraData = ref renderingData.cameraData;

        RenderTargetIdentifier cameraTarget = (cameraData.targetTexture != null) ? new RenderTargetIdentifier(cameraData.targetTexture) : BuiltinRenderTextureType.CurrentActive;

        if (cameraData.isSceneViewCamera)
        {
            cmd.Blit(colorTarget, targetHandle.Identifier(), _blitLightsMaterial);
            cmd.Blit(targetHandle.Identifier(), colorTarget);

            // cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            // cmd.SetViewport(cameraData.camera.pixelRect);
            // cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, _blitLightsMaterial);
            // cmd.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, cameraData.camera.projectionMatrix);

            // cmd.SetRenderTarget(cameraTarget,
                    // RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,     // color
                    // RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare); // depth
            // cmd.Blit(BuiltinRenderTextureType.None, cameraTarget, _blitLightsMaterial);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    
}