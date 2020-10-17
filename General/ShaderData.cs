
using System.Runtime.InteropServices;
using UnityEngine;
using static DeferredLightsFeature;

public class ShaderData : System.IDisposable
{
    static ShaderData _instance = null;

    ComputeBuffer lightsDataBuffer;
    ComputeBuffer pixelDataBuffer;

    ComputeBuffer lightIndexBuffer;
    ComputeBuffer lightIndexCounterBuffer;
    ComputeBuffer frustumDataBuffer;

    ShaderData() { }

    internal static ShaderData instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ShaderData();
            }

            return _instance;
        }
    }

    public ComputeBuffer GetLightsDataBuffer(int size = 0)
    {
        return GetOrUpdateBuffer(ref lightsDataBuffer, size, Marshal.SizeOf<LightData>(), "LightsDataBuffer");
    }

    public ComputeBuffer GetPixelDataBuffer(int size = 0)
    {
        return GetOrUpdateBuffer(ref pixelDataBuffer, size, Marshal.SizeOf<PixelData>(), "PixelDataBuffer");
    }

    public ComputeBuffer GetLightIndexBuffer(int size = 0)
    {
        return GetOrUpdateBuffer(ref lightIndexBuffer, size, sizeof(uint) * 1, "LightIndexBuffer");
    }

    public ComputeBuffer GetLightIndexCounterBuffer(int size = 0)
    {
        return GetOrUpdateBuffer(ref lightIndexCounterBuffer, size, sizeof(uint) * 1, "LightIndexCounterBuffer");
    }

    public ComputeBuffer GetFrustumDataBuffer(int size = 0)
    {
        return GetOrUpdateBuffer(ref frustumDataBuffer, size, sizeof(float) * 4 * 4, "FurstumDataBuffer");
    }

    ComputeBuffer GetOrUpdateBuffer(ref ComputeBuffer buffer, int size, int bytes, string name)
    {
        if (size == 0) return buffer;

        if (buffer == null)
        {
            buffer = new ComputeBuffer(size, bytes);
            buffer.name = name;
        }
        else if (buffer.count < size)
        {
            buffer.Dispose();
            buffer = new ComputeBuffer(size, bytes);
            buffer.name = name;
        }

        return buffer;
    }

    public void Dispose()
    {
        DisposeBuffer(ref lightsDataBuffer);
        DisposeBuffer(ref pixelDataBuffer);

        DisposeBuffer(ref lightIndexBuffer);
        DisposeBuffer(ref lightIndexCounterBuffer);
        DisposeBuffer(ref frustumDataBuffer);
    }

    public static void DisposeBuffer(ref ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            buffer.Dispose();
            buffer = null;
        }
    }
}