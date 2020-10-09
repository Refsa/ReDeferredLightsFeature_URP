using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;
using LightData = DeferredLightsFeature.LightData;
using System.Linq;
using System.Collections.Generic;

public class DeferredTilesPass : ScriptableRenderPass
{
    public class DeferredTilesBuffers : System.IDisposable
    {
        public ComputeBuffer LightIndexBuffer {get; private set;}
        public ComputeBuffer LightIndexCounterBuffer {get; private set;}
        public ComputeBuffer FrustumDataBuffer {get; private set;}

        public void Dispose()
        {
            LightIndexBuffer?.Release();
            LightIndexCounterBuffer?.Release();
            FrustumDataBuffer?.Release();
        }

        public DeferredTilesBuffers()
        {
            if (FrustumDataBuffer == null)
            {
                FrustumDataBuffer = new ComputeBuffer(MAX_TILES, sizeof(float) * 4 * 4);
                FrustumDataBuffer.name = "FrustumDataBuffer";
            }
            if (LightIndexBuffer == null)
            {
                LightIndexBuffer = new ComputeBuffer(MAX_LIGHTS_PER_TILE, sizeof(uint) * 1);
                LightIndexBuffer.name = "LightIndexBuffer";
            }
            if (LightIndexCounterBuffer == null)
            {
                LightIndexCounterBuffer = new ComputeBuffer(1, sizeof(uint) * 1);
                LightIndexCounterBuffer.name = "LightIndexCounterBuffer";
            }
        }
    }

    const string TILE_DATA_ID = "_TileData";
    const string FRUSTUM_DATA_ID = "_Frustum";
    const string LIGHT_DATA_ID = "_LightData";
    const string LIGHT_INDEX_ID = "_LightIndexData";
    const string LIGHT_INDEX_COUNTER_ID = "_LightIndexCounter";
    const int TILE_SIZE = 16;
    const float TILE_SIZE_FLOAT = (float)TILE_SIZE;
    const int MAX_TILES = ((2560 * 1440) / (TILE_SIZE * TILE_SIZE));
    const int MAX_LIGHTS_PER_TILE = MAX_TILES * 256;

    ComputeShader tilesCompute;
    ComputeShader lightsCompute;

    DeferredTilesBuffers computeBuffers;
    ComputeBuffer lightDataBuffer;

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

    public void SetBuffers(ref ComputeBuffer lightDataBuffer, ref DeferredTilesBuffers buffers)
    {
        this.lightDataBuffer = lightDataBuffer;
        this.computeBuffers = buffers;
    }
 
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int textureWidth = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int textureHeight = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        width = Mathf.CeilToInt(((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier) / TILE_SIZE_FLOAT); // g
        height = Mathf.CeilToInt(((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier) / TILE_SIZE_FLOAT); // g

        int newTileDispatchX = Mathf.CeilToInt(width / TILE_SIZE_FLOAT);  // G
        int newTileDispatchY = Mathf.CeilToInt(height / TILE_SIZE_FLOAT); // G

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
        cmd.GetTemporaryRT(tileDataHandle.id, tileDataDescriptor);

        cmd.SetComputeIntParam(tilesCompute, "_LightCount", DeferredLightsPass.LightCount);
        cmd.SetComputeTextureParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, TILE_DATA_ID, tileDataHandle.Identifier());
        cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, FRUSTUM_DATA_ID, computeBuffers.FrustumDataBuffer);
        cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, LIGHT_DATA_ID, lightDataBuffer);
        cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, LIGHT_INDEX_ID, computeBuffers.LightIndexBuffer);
        cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, LIGHT_INDEX_COUNTER_ID, computeBuffers.LightIndexCounterBuffer);

        cmd.SetComputeBufferParam(lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, LIGHT_INDEX_ID, computeBuffers.LightIndexBuffer);
        cmd.SetComputeTextureParam(lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, TILE_DATA_ID, tileDataHandle.Identifier());

        cmd.SetGlobalTexture(TILE_DATA_ID, tileDataHandle.Identifier());

        // ### COMPUTE FRUSTUMS ###
        // if (refreshTiles)
        {
            cmd.SetComputeBufferParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeTileFrustumKernelID, FRUSTUM_DATA_ID, computeBuffers.FrustumDataBuffer);
        }
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

            cmd.SetComputeVectorParam(tilesCompute, "_InputSize", new Vector2(camera.pixelWidth, camera.pixelHeight));
            cmd.SetComputeVectorParam(tilesCompute, "_ProjParams", new Vector4(
                    1f, camera.nearClipPlane, camera.farClipPlane, 1f / camera.farClipPlane
                ));
            cmd.SetComputeMatrixParam(tilesCompute, "_MVP", camera.projectionMatrix * camera.worldToCameraMatrix);
            cmd.SetComputeMatrixParam(tilesCompute, "MATRIX_IP", camera.projectionMatrix.inverse);
            cmd.SetComputeMatrixParam(tilesCompute, "MATRIX_V", camera.worldToCameraMatrix);
            cmd.SetComputeMatrixParam(tilesCompute, "MATRIX_IV", camera.cameraToWorldMatrix);
            cmd.SetComputeVectorParam(tilesCompute, "_NumThreads", new Vector2(width, height));
            cmd.SetComputeVectorParam(tilesCompute, "_NumThreadGroups", new Vector2(tileDispatchX, tileDispatchY));
        }

        // ### COMPUTE FRUSTUMS ###
        // if (refreshTiles)
        {
            cmd.BeginSample("DeferredLightsPass: Compute Tile Frustums");
            cmd.DispatchCompute(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeTileFrustumKernelID, width, height, 1);
            cmd.EndSample("DeferredLightsPass: Compute Tile Frustums");
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

    struct Plane {
        public Vector3 Normal;
        public float Distance;
    }

    struct Frustum {
        public Plane Plane1;
        public Plane Plane2;
        public Plane Plane3;
        public Plane Plane4;
    }

    struct FrustumEx {
        public Plane[] planes;
    }

    Frustum[] frustumData = new Frustum[MAX_TILES];

    public override void FrameCleanup(CommandBuffer cmd)
    {   
        // int tiles = width * height;
        // computeBuffers.FrustumDataBuffer.GetData(frustumData, 0, 0, tiles);

        // List<FrustumEx> frustums = new List<FrustumEx>();
        // for (int i = 0; i < tiles; i++)
        // {
        //     var frustum = frustumData[i];
        //     var frustumEx = new FrustumEx{planes = new Plane[4]};
        //     frustumEx.planes[0] = frustum.Plane1; frustumEx.planes[1] = frustum.Plane2; frustumEx.planes[2] = frustum.Plane3; frustumEx.planes[3] = frustum.Plane4;
        //     bool distinctPlanes = frustumEx.planes.Distinct().Count() == 4;
        //     UnityEngine.Debug.Assert(distinctPlanes, $"planes in frustum was not unique");
        // }

        // int uniques = frustums.Distinct().Count();
        // UnityEngine.Debug.Assert(uniques == tiles, $"dupes of frustums found: {tiles - uniques}");

        this.lightDataBuffer = null;
        this.computeBuffers = null;
        cmd.ReleaseTemporaryRT(tileDataHandle.id);
    }
}