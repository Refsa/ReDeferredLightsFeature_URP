using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class DeferredLightsPass : ScriptableRenderPass
{
    const string lightsID = "_DeferredLightsTexture";
    const string colorFullscreenID = "_DeferredOutputTexture";

    RenderTargetHandle lightsHandle;
    RenderTargetHandle colorFullscreenHandle;

    ComputeShader _lightsCompute;
    Settings _settings;

    Vector2 passSize;
    Vector2 renderSize;

    Material _blitLightsMaterial;

    RenderTargetIdentifier colorTarget;

    public RenderTargetHandle BlitHandle => colorFullscreenHandle;
    public RenderTargetHandle LightsHandle => lightsHandle;

    public DeferredLightsPass(Settings settings)
    {
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        _settings = settings;
        _lightsCompute = ComputeShaderUtils.LightsCompute;

        // ### SETUP RENDER TEXTURE HANDLES ###
        {
            lightsHandle.Init(lightsID);

            colorFullscreenHandle.Init(colorFullscreenID);
        }
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
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        passSize = new Vector2(width, height);
        renderSize = new Vector2(cameraTextureDescriptor.width, cameraTextureDescriptor.height);

        cmd.BeginSample("DeferredLightsPass: Setup");

        // Lights RT
        {
            RenderTextureDescriptor rtd = new RenderTextureDescriptor(width, height);
            rtd.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
            rtd.depthBufferBits = 0;
            rtd.enableRandomWrite = true;
            cmd.GetTemporaryRT(lightsHandle.id, rtd);
            ComputeShaderUtils.Utils.DispatchClear(cmd, lightsHandle.Identifier(), width, height, Color.black);
        }

        // Full size RT
        {
            var rtd = cameraTextureDescriptor;
            rtd.enableRandomWrite = true;
            cmd.GetTemporaryRT(colorFullscreenHandle.id, rtd);
            ComputeShaderUtils.Utils.DispatchClear(cmd, colorFullscreenHandle.Identifier(), width, height, Color.black);
        }

        // ### COMPUTE GLOBALS ###
        {
            cmd.SetComputeVectorParam(_lightsCompute, "_InputSize", passSize);
            cmd.SetComputeVectorParam(_lightsCompute, "_OutputSize", renderSize);
            cmd.SetComputeFloatParam(_lightsCompute, "_RenderScale", _settings.ResolutionMultiplier);
        }

        // ### COLOR DOWNSAMPLE ###
        {
            cmd.Blit(colorAttachment, colorFullscreenHandle.Identifier());
        }

        // ### OUTPUT UPSAMPLE ###
        {
            cmd.SetComputeTextureParam(
                _lightsCompute,
                ComputeShaderUtils.LightsComputeKernels.UpsampleOutputKernelID,
                lightsID, lightsHandle.Identifier());

            cmd.SetComputeTextureParam(_lightsCompute,
                ComputeShaderUtils.LightsComputeKernels.UpsampleOutputKernelID,
                colorFullscreenID, colorFullscreenHandle.Identifier());
        }

        // ### SETUP LIGHT COMPUTE ###
        {
            var lightDataBuffer = ShaderData.instance.GetCullLightsOutputBuffer();

            cmd.SetComputeIntParam(_lightsCompute, "_LightCount", CullLightsHandler.LightCount);
            cmd.SetComputeBufferParam(_lightsCompute,
                ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID,
                "_LightData", lightDataBuffer);

            cmd.SetComputeTextureParam(_lightsCompute,
                ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID,
                lightsID, lightsHandle.Identifier());
        }

        // Create global data
        {
            var lightDataBuffer = ShaderData.instance.GetCullLightsOutputBuffer();
            cmd.SetGlobalBuffer("_LightData", lightDataBuffer);
        }

        cmd.EndSample("DeferredLightsPass: Setup");
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("DeferredLights");

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        cmd.BeginSample("DeferredLightsPass: Execute");


        ref CameraData cameraData = ref renderingData.cameraData;
        RenderTargetIdentifier cameraTarget = (cameraData.targetTexture != null) ? new RenderTargetIdentifier(cameraData.targetTexture) : colorTarget;
        cmd.Blit(colorTarget, colorFullscreenHandle.Identifier());

        // ### SET CAMERA DATA ###
        {
            Camera camera = cameraData.camera;
            cmd.SetComputeVectorParam(_lightsCompute, "_CameraPos", camera.transform.position);
            cmd.SetComputeMatrixParam(_lightsCompute, "MATRIX_IV", camera.cameraToWorldMatrix);
            cmd.SetComputeVectorParam(_lightsCompute, "_ProjParams", new Vector4(
                1f, camera.nearClipPlane, camera.farClipPlane, 1f / camera.farClipPlane
            ));
        }
        // ### DISPATCH LIGHT COMPUTE ###
        {
            cmd.BeginSample("DeferredLightsPass: Compute Lights");
            cmd.DispatchCompute(_lightsCompute,
                ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID,
                (int)passSize.x / 32, (int)passSize.y / 18, 1);
            cmd.EndSample("DeferredLightsPass: Compute Lights");
        }
        // ### BLUR LIGHTS TEXTURE ###
        {
            // cmd.BeginSample("DeferredLightsPass: Blur Lights");
            // ComputeShaderUtils.Utils.DispatchGaussianBlur(cmd, lightsHandle.Identifier(), (int)passSize.x / 32, (int)passSize.y / 18);
            // cmd.EndSample("DeferredLightsPass: Blur Lights");
        }
        // ### UPSAMPLE LIGHTS TEXTURE ###
        {
            cmd.BeginSample("DeferredLightsPass: Upsample Output");
            cmd.DispatchCompute(_lightsCompute,
                ComputeShaderUtils.LightsComputeKernels.UpsampleOutputKernelID,
                (int)passSize.x / 32, (int)passSize.y / 18, 1);
            cmd.EndSample("DeferredLightsPass: Upsample Output");
        }

        cmd.SetGlobalTexture("_LightsTexture", lightsHandle.Identifier());

        if (cameraData.isSceneViewCamera && _settings.ShowInSceneView)
        {
            cmd.Blit(colorFullscreenHandle.Identifier(), colorTarget);
        }
        else if (cameraData.isDefaultViewport)
        {
            cmd.Blit(colorFullscreenHandle.Identifier(), colorTarget);
        }

        cmd.EndSample("DeferredLightsPass: Execute");

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(lightsHandle.id);
        cmd.ReleaseTemporaryRT(colorFullscreenHandle.id);
    }
}