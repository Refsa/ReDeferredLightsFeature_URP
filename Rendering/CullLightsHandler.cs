using UnityEngine;

public class CullLightsHandler
{
    public CullLightsHandler()
    {
        lightDatas = new LightData[DeferredLightsFeature.MAX_LIGHTS];
    }

    LightData[] lightDatas;
    int lightCount = 0;
    public static int LightCount;

    Vector4[] lastFrustumPlanes = new Vector4[6];
    UnityEngine.Plane[] tempPlanes = new UnityEngine.Plane[6];

    int[] lightCounter = new int[1] { 0 };

    public void CullLights(Camera camera)
    {
        PrepareLightDataBuffer();
        CreateCameraFrustum(camera, ref lastFrustumPlanes);

        var cullCompute = ComputeShaderUtils.CullLightsCompute;
        var lightDataBuffer = ShaderData.instance.GetLightsDataBuffer();
        var outputDataBuffer = ShaderData.instance.GetCullLightsOutputBuffer(DeferredLightsFeature.MAX_LIGHTS);

        outputDataBuffer.SetCounterValue(0u);

        cullCompute.SetBuffer(ComputeShaderUtils.CullLightsKernels.CullLightsKernelID, "_LightDataInput", lightDataBuffer);
        cullCompute.SetBuffer(ComputeShaderUtils.CullLightsKernels.CullLightsKernelID, "_LightDataOutput", outputDataBuffer);
        cullCompute.SetVectorArray("_CameraFrustum", lastFrustumPlanes);
        cullCompute.SetInt("_LightCount", lightCount);
        cullCompute.SetVector("_CameraClips", new Vector2(camera.nearClipPlane, camera.farClipPlane));

        int batchSize = Mathf.Max(Mathf.CeilToInt((float)lightCount / 64f), 1);
        cullCompute.Dispatch(ComputeShaderUtils.CullLightsKernels.CullLightsKernelID, batchSize, 1, 1);

        var lightCountBuffer = ShaderData.instance.GetLightCountReadBuffer();
        ComputeBuffer.CopyCount(outputDataBuffer, lightCountBuffer, 0);
        lightCountBuffer.GetData(lightCounter);

        LightCount = lightCounter[0];

        // UnityEngine.Debug.Log($"{camera.name}: In View: {LightCount} - Batch Size: {batchSize} - Lights In Scene: {lightCount}");
    }

    void PrepareLightDataBuffer()
    {
        // ### GET ALL LIGHTS IN SCENE ###
        lightCount = 0;
        foreach (var ld in GameObject.FindObjectsOfType<DeferredLightsData>())
        {
            if (!ld.gameObject.activeSelf) continue;

            lightDatas[lightCount].Position = ld.transform.position;
            lightDatas[lightCount].Color = new Vector3(ld.Color.r, ld.Color.g, ld.Color.b);
            lightDatas[lightCount].Attenuation = ld.Attenuation;
            lightDatas[lightCount].RangeSqr = ld.RangeSqr;
            lightDatas[lightCount].Range = ld.Range;

            lightCount++;
        }

        var lightDataBuffer = ShaderData.instance.GetLightsDataBuffer(DeferredLightsFeature.MAX_LIGHTS);
        lightDataBuffer.SetData(lightDatas);
    }

    void CreateCameraFrustum(Camera camera, ref Vector4[] frustum)
    {
        GeometryUtility.CalculateFrustumPlanes(camera, tempPlanes);

        frustum[0] = new Vector4(tempPlanes[0].normal.x, tempPlanes[0].normal.y, tempPlanes[0].normal.z, tempPlanes[0].distance);
        frustum[1] = new Vector4(tempPlanes[1].normal.x, tempPlanes[1].normal.y, tempPlanes[1].normal.z, tempPlanes[1].distance);
        frustum[2] = new Vector4(tempPlanes[2].normal.x, tempPlanes[2].normal.y, tempPlanes[2].normal.z, tempPlanes[2].distance);
        frustum[3] = new Vector4(tempPlanes[3].normal.x, tempPlanes[3].normal.y, tempPlanes[3].normal.z, tempPlanes[3].distance);
        frustum[4] = new Vector4(tempPlanes[4].normal.x, tempPlanes[4].normal.y, tempPlanes[4].normal.z, tempPlanes[4].distance);
        frustum[5] = new Vector4(tempPlanes[5].normal.x, tempPlanes[5].normal.y, tempPlanes[5].normal.z, tempPlanes[5].distance);
    }
}
