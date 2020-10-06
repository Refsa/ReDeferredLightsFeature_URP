using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;
using LightData = DeferredLightsFeature.LightData;

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

    ComputeBuffer lightDataBuffer;
    LightData[] lightDatas;
    int lightCount;

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

        PrepareBuffers();
    } 
 
    public void PrepareBuffers() 
    {
        lightDatas = new LightData[DeferredLightsFeature.MAX_LIGHTS];
    }

    public void SetBuffer(ref ComputeBuffer lightsBuffer)
    {
        lightDataBuffer = lightsBuffer;
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

            int size = 2048;
            if (width > size) size = 4096;
            rtd.width = size;
            rtd.height = size;
            rtd.colorFormat = RenderTextureFormat.ARGBFloat;
            rtd.depthBufferBits = 0;
            rtd.enableRandomWrite = true;
            cmd.GetTemporaryRT(lightsHandle.id, rtd);
        }

        // Full size RT
        {
            var rtd = cameraTextureDescriptor;
            rtd.enableRandomWrite = true;
            cmd.GetTemporaryRT(colorFullscreenHandle.id, rtd);
        }

        // ### GET ALL LIGHTS IN SCENE ###        
        lightCount = 0;
        foreach (var ld in GameObject.FindObjectsOfType<DeferredLightsData>())
        {
            lightDatas[lightCount].Position = ld.transform.position;
            lightDatas[lightCount].Color = new Vector3(ld.Color.r, ld.Color.g, ld.Color.b);
            lightDatas[lightCount].Intensity = ld.Intensity;
            lightDatas[lightCount].Range = ld.Range;

            lightCount++;
        }
        // UnityEngine.Debug.Log($"{lightCount}");

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
            cmd.SetComputeTextureParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.UpsampleOutputKernelID, lightsID, lightsHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.UpsampleOutputKernelID, colorFullscreenID, colorFullscreenHandle.Identifier());
        }

        // ### SETUP LIGHT COMPUTE ###
        {
            lightDataBuffer.SetData(lightDatas, 0, 0, lightCount);
            cmd.SetComputeIntParam(_lightsCompute, "_LightCount", lightCount);
            cmd.SetComputeBufferParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, "_LightData", lightDataBuffer);
            cmd.SetComputeTextureParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, lightsID, lightsHandle.Identifier());
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

        // ### SET CAMERA DATA ###
        {
            Camera camera = cameraData.camera;
            cmd.SetComputeVectorParam(_lightsCompute, "_CameraPos", camera.transform.position);
            cmd.SetComputeMatrixParam(_lightsCompute, "MATRIX_IV", camera.cameraToWorldMatrix);
            cmd.SetComputeVectorParam(_lightsCompute, "_ProjectionParams", new Vector4(
                1f, camera.nearClipPlane, camera.farClipPlane, 1f / camera.farClipPlane
            ));
        }
        // ### DISPATCH LIGHT COMPUTE ###
        {
            cmd.BeginSample("DeferredLightsPass: Compute Lights");
            cmd.DispatchCompute(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, (int)passSize.x / 32, (int)passSize.y / 18, 1);
            cmd.EndSample("DeferredLightsPass: Compute Lights");
        }
        // ### BLUR LIGHTS TEXTURE ###
        {
            cmd.BeginSample("DeferredLightsPass: Blur Lights");
            ComputeShaderUtils.Utils.DispatchGaussianBlur(cmd, lightsHandle.Identifier(), (int)passSize.x / 32, (int)passSize.y / 18);
            cmd.EndSample("DeferredLightsPass: Blur Lights");
        }
        // ### UPSAMPLE LIGHTS TEXTURE ###
        {
            cmd.BeginSample("DeferredLightsPass: Upsample Output");
            cmd.DispatchCompute(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.UpsampleOutputKernelID, (int)passSize.x / 32, (int)passSize.y / 18, 1);
            cmd.EndSample("DeferredLightsPass: Upsample Output");
        }

        // ### BLIT RESULTS BACK INTO RENDER BUFFER ###
        if (cameraData.isDefaultViewport)
        {
            cmd.SetRenderTarget(
                BuiltinRenderTextureType.CurrentActive,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, // color
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare); // depth

            cmd.Blit(colorFullscreenHandle.id, BuiltinRenderTextureType.CurrentActive);
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