using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class AlbedoGrabPass : ScriptableRenderPass
{
    const string ALBEDO_ID = "_AlbedoTexture";

    static ShaderTagId s_ShaderTagLit = new ShaderTagId("Lit");
    static ShaderTagId s_ShaderTagSimpleLit = new ShaderTagId("SimpleLit");
    static ShaderTagId s_ShaderTagUnlit = new ShaderTagId("Unlit");
    static ShaderTagId s_ShaderTagUniversalGBuffer = new ShaderTagId("UniversalGBuffer");
    static ShaderTagId s_ShaderTagUniversalMaterialType = new ShaderTagId("UniversalMaterialType");

    RenderTargetHandle albedoHandle;

    FilteringSettings filteringSettings;
    RenderStateBlock renderStateBlock;

    Settings _settings;
    ComputeShader _lightsCompute;
    Material _albedoGrabMaterial;

    public AlbedoGrabPass(Settings settings, ComputeShader lightsCompute, Material albedoGrabMaterial)
    {
        _settings = settings;
        _lightsCompute = lightsCompute;
        _albedoGrabMaterial = albedoGrabMaterial;

        renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);

        renderStateBlock = new RenderStateBlock();

        albedoHandle.Init(ALBEDO_ID);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        var rtd = cameraTextureDescriptor;
        rtd.colorFormat = RenderTextureFormat.ARGB32;
        rtd.width = width;
        rtd.height = height;
        rtd.depthBufferBits = 32;
        rtd.msaaSamples = 1;
        cmd.GetTemporaryRT(albedoHandle.id, rtd, FilterMode.Point);

        ConfigureTarget(albedoHandle.Identifier());
        ConfigureClear(ClearFlag.None, Color.black);

        cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.ComputeLightsKernelID, ALBEDO_ID, albedoHandle.Identifier());
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        using (new ProfilingScope(cmd, new ProfilingSampler("DeferredLightsPass: Grab Albedo")))
        {
            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
            var drawSettings = CreateDrawingSettings(s_ShaderTagUniversalMaterialType, ref renderingData, sortFlags);
            drawSettings.overrideMaterial = _albedoGrabMaterial;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStateBlock);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (albedoHandle != RenderTargetHandle.CameraTarget)
        {
            cmd.ReleaseTemporaryRT(albedoHandle.id);
        }
    }
}