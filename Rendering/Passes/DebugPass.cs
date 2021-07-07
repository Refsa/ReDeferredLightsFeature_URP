using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class DebugPass : ScriptableRenderPass
{
    Settings _settings;
    Material _debugMaterial;

    RenderTargetIdentifier colorTarget;
    RenderTargetHandle tempHandle;

    static Texture2D heatmapTexture;

    public DebugPass(Settings settings, Material debugMaterial)
    {
        _settings = settings;
        _debugMaterial = debugMaterial;

        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        tempHandle.Init("_DebugPassTemp");

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
        cmd.GetTemporaryRT(tempHandle.id, cameraTextureDescriptor);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get("_DeferredDebugPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        ref CameraData cameraData = ref renderingData.cameraData;

#if UNITY_EDITOR
        cmd.SetGlobalInt("_DebugMode", (int)_settings.DebugMode);
#endif
        var debugMode = (DeferredLightsFeature.DebugMode)Shader.GetGlobalInt("_DebugMode");

        if (debugMode != DeferredLightsFeature.DebugMode.None)
        {
            cmd.SetGlobalMatrix("MATRIX_IV", cameraData.camera.cameraToWorldMatrix);
            cmd.SetGlobalTexture("_HeatmapTexture", heatmapTexture);

            Material debugMaterial = _debugMaterial;

            if (cameraData.isSceneViewCamera && _settings.DebugModeInSceneView)
            {
                cmd.Blit(colorTarget, tempHandle.Identifier(), debugMaterial);
                cmd.Blit(tempHandle.Identifier(), colorTarget);
            }
            else if ((cameraData.isDefaultViewport) && !cameraData.isSceneViewCamera)
            {
                cmd.Blit(colorTarget, tempHandle.Identifier(), debugMaterial);
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
