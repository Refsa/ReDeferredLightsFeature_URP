using UnityEngine;
using UnityEngine.Rendering;

public static class ComputeShaderUtils
{
    public static ComputeShader LightsCompute;
    public static ComputeShader UtilsCompute;
    public static ComputeShader TilesCompute;
    public static ComputeShader CullLightsCompute;

    public static class LightsComputeKernels
    {
        public static int UpsampleOutputKernelID {get; private set;} = -1;
        public static int ComputeLightsKernelID {get; private set;} = -1;
        public static int ComputePixelDataKernelID {get; private set;} = -1;

        internal static void Prepare()
        {
            UpsampleOutputKernelID = LightsCompute.FindKernel("UpsampleOutput");
            ComputeLightsKernelID = LightsCompute.FindKernel("ComputeLights");
            ComputePixelDataKernelID = LightsCompute.FindKernel("ComputePixelData");
        }
    }

    public static class UtilsComputeKernels
    {
        public static int ClearTextureKernelID {get; private set;} = -1;
        public static int GaussianBlurKernelID {get; private set;} = -1;

        internal static void Prepare()
        {
            ClearTextureKernelID = UtilsCompute.FindKernel("ClearTexture");
            GaussianBlurKernelID = UtilsCompute.FindKernel("GaussianBlurTexture");
        }
    }

    public static class TilesComputeKernels
    {
        public static int ComputeLightTilesKernelID {get; private set;} = -1;
        public static int ComputeTileFrustumKernelID {get; private set;} = -1;

        internal static void Prepare()
        {
            ComputeLightTilesKernelID = TilesCompute.FindKernel("ComputeLightTiles");
            ComputeTileFrustumKernelID = TilesCompute.FindKernel("ComputeTileFrustums");
        }
    }

    public static class Utils
    {
        public static void DispatchGaussianBlur(CommandBuffer cmd, RenderTargetIdentifier texture, int width, int height)
        {
            cmd.SetComputeTextureParam(UtilsCompute, UtilsComputeKernels.GaussianBlurKernelID, "_TargetTexture", texture);
            cmd.DispatchCompute(UtilsCompute, UtilsComputeKernels.GaussianBlurKernelID, width, height, 1);
        }

        public static void DispatchClear(CommandBuffer command, RenderTargetIdentifier texture, int width, int height, Color color)
        {
            command.SetComputeTextureParam(UtilsCompute, UtilsComputeKernels.ClearTextureKernelID, "_TargetTexture", texture);
            command.SetComputeVectorParam(UtilsCompute, "_ClearColor", color);
            command.DispatchCompute(UtilsCompute, UtilsComputeKernels.ClearTextureKernelID, width / 32, height / 32, 1);
        }
    }

    public static class CullLightsKernels
    {
        public static int CullLightsKernelID {get; private set;} = -1;

        internal static void Prepare()
        {
            CullLightsKernelID = CullLightsCompute.FindKernel("CullLights");
        }
    }

    public static bool Prepare()
    {
        bool error = false;

        if (LightsCompute == null) error = !PrepareCompute("Compute/DeferredLightsCompute", ref LightsCompute);
        if (!error) LightsComputeKernels.Prepare();
        else 
        {
            UnityEngine.Debug.LogError($"ReLights URP: Couldn't load DeferredLightsCompute");
            return error;
        }

        if (UtilsCompute == null) error = !PrepareCompute("Compute/Utils", ref UtilsCompute);
        if (!error) UtilsComputeKernels.Prepare();
        else 
        {
            UnityEngine.Debug.LogError($"ReLights URP: Couldn't load UtilsCompute");
            return error;
        }

        if (TilesCompute == null) error = !PrepareCompute("Compute/DeferredTiles", ref TilesCompute);
        if (!error) TilesComputeKernels.Prepare();
        else 
        {
            UnityEngine.Debug.LogError($"ReLights URP: Couldn't load TilesCompute");
            return error;
        }

        if (CullLightsCompute == null) error = !PrepareCompute("Compute/CullLightsCompute", ref CullLightsCompute);
        if (!error) CullLightsKernels.Prepare();
        else
        {
            UnityEngine.Debug.LogError($"ReLights URP: Couldn't load CullLightsCompute");
            return error;
        }

        return error;
    }

    static bool PrepareCompute(string path, ref ComputeShader field)
    {
        if (field == null)
        {
            field = Resources.Load<ComputeShader>(path);
        }
        if (field == null)
        {
            UnityEngine.Debug.LogError($"Could not find compute shader at path: {path}");
            return false;
        }

        return true;
    }
}