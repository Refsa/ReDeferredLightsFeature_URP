using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;
using System.Linq;
using System.Collections.Generic;

public class DeferredTilesPass : ScriptableRenderPass
{
    const string TILE_DATA_ID = "_TileData";
    const string FRUSTUM_DATA_ID = "_Frustum";
    const string LIGHT_DATA_ID = "_LightData";
    const string LIGHT_INDEX_ID = "_LightIndexData";
    const string LIGHT_INDEX_COUNTER_ID = "_LightIndexCounter";
    const int TILE_SIZE = 16;
    const float TILE_SIZE_FLOAT = (float)TILE_SIZE;
    const int MAX_TILES = ((2560 * 1440) / (TILE_SIZE * TILE_SIZE));
    const int MAX_LIGHTS_PER_TILE = MAX_TILES * TILE_SIZE * 256;

    uint[] tileIndexCounterClear = new uint[1] { 0u };

    ComputeShader tilesCompute;
    ComputeShader lightsCompute;

    RenderTargetHandle tileDataHandle;

    Settings _settings;

    int tileDispatchX, tileDispatchY;
    int width, height;
    bool refreshTiles;

    public DeferredTilesPass(Settings settings)
    {
        _settings = settings;

        lightsCompute = ComputeShaderUtils.LightsCompute;
        tilesCompute = ComputeShaderUtils.TilesCompute;

        tileDataHandle.Init(TILE_DATA_ID);

        renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int textureWidth = cameraTextureDescriptor.width;
        int textureHeight = cameraTextureDescriptor.height;

        width = Mathf.CeilToInt((float)textureWidth / TILE_SIZE_FLOAT); // g
        height = Mathf.CeilToInt((float)textureHeight / TILE_SIZE_FLOAT); // g

        int newTileDispatchX = Mathf.CeilToInt((float)width / TILE_SIZE_FLOAT);  // G
        int newTileDispatchY = Mathf.CeilToInt((float)height / TILE_SIZE_FLOAT); // G

        refreshTiles = newTileDispatchX != tileDispatchX || newTileDispatchY != tileDispatchY;
        tileDispatchX = newTileDispatchX; // G
        tileDispatchY = newTileDispatchY; // G

        var tileDataDescriptor = new RenderTextureDescriptor();
        tileDataDescriptor.width = width;
        tileDataDescriptor.height = height;
        tileDataDescriptor.dimension = TextureDimension.Tex2D;
        tileDataDescriptor.msaaSamples = 1;
        tileDataDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_UInt;
        tileDataDescriptor.enableRandomWrite = true;
        cmd.GetTemporaryRT(tileDataHandle.id, tileDataDescriptor, FilterMode.Point);

        var frustumDataBuffer = ShaderData.instance.GetFrustumDataBuffer(MAX_TILES);
        var lightDataBuffer = ShaderData.instance.GetCullLightsOutputBuffer();
        var lightIndexCounterBuffer = ShaderData.instance.GetLightIndexCounterBuffer(1);
        var lightIndexBuffer = ShaderData.instance.GetLightIndexBuffer(MAX_LIGHTS_PER_TILE);

        cmd.SetComputeVectorParam(tilesCompute, "_InputSize", new Vector2(cameraTextureDescriptor.width, cameraTextureDescriptor.height));
        cmd.SetComputeIntParam(tilesCompute, "_LightCount", CullLightsHandler.LightCount);
        cmd.SetComputeTextureParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, TILE_DATA_ID, tileDataHandle.Identifier());
        cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, FRUSTUM_DATA_ID + "_static", frustumDataBuffer);
        cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, LIGHT_DATA_ID, lightDataBuffer);
        cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, LIGHT_INDEX_ID, lightIndexBuffer);
        cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, LIGHT_INDEX_COUNTER_ID, lightIndexCounterBuffer);

        cmd.SetComputeBufferParam(lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, LIGHT_INDEX_ID + "_static", lightIndexBuffer);
        cmd.SetComputeTextureParam(lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, TILE_DATA_ID + "_static", tileDataHandle.Identifier());

        cmd.SetGlobalTexture(TILE_DATA_ID, tileDataHandle.Identifier());
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        // ### SET CBUFFER PARAMS ###
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            var ipmat = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false).inverse;

            cmd.SetComputeVectorParam(tilesCompute, "_CameraPos", camera.transform.position);
            cmd.SetComputeVectorParam(tilesCompute, "_ProjParams", new Vector4(
                    1f, camera.nearClipPlane, camera.farClipPlane, 1f / camera.farClipPlane
                ));
            cmd.SetComputeMatrixParam(tilesCompute, "_MVP", camera.projectionMatrix * camera.worldToCameraMatrix);
            cmd.SetComputeMatrixParam(tilesCompute, "MATRIX_IP", ipmat);
            cmd.SetComputeMatrixParam(tilesCompute, "MATRIX_V", camera.worldToCameraMatrix);
            cmd.SetComputeMatrixParam(tilesCompute, "MATRIX_IV", camera.cameraToWorldMatrix);
            cmd.SetComputeVectorParam(tilesCompute, "_NumThreads", new Vector2(width, height));
            cmd.SetComputeVectorParam(tilesCompute, "_NumThreadGroups", new Vector2(tileDispatchX, tileDispatchY));
        }

        // ### RESET DATA ###
        {
            var lightIndexCounterBuffer = ShaderData.instance.GetLightIndexCounterBuffer(1);

            lightIndexCounterBuffer.SetData(tileIndexCounterClear);
        }

        // ### COMPUTE FRUSTUMS ###
        // if (refreshTiles)
        {
            var frustumDataBuffer = ShaderData.instance.GetFrustumDataBuffer(MAX_TILES);
            cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeTileFrustumKernelID, FRUSTUM_DATA_ID, frustumDataBuffer);
            cmd.DispatchCompute(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeTileFrustumKernelID, tileDispatchX, tileDispatchY, 1);
        }

        // ### COMPUTE PER TILE LIGHT DATA ###
        {
            cmd.BeginSample("DeferredLightsPass: Compute Tile Indices");
            cmd.DispatchCompute(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, width, height, 1);
            cmd.EndSample("DeferredLightsPass: Compute Tile Indices");
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(tileDataHandle.id);
    }
}