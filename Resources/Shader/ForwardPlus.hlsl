#ifndef BLOCK_SIZE
#define BLOCK_SIZE 16
#endif

#ifndef SHADERGRAPH_PREVIEW
struct PixelData
{
    float3 Diffuse;
    float3 Normal;
    float Depth;
    float3 ViewDir;
    float3 Position;
    float3 Specular;
    float Roughness;
    float Roughness2;
    float Roughness2MinusOne;
    float NormalizationTerm;
};

struct DFLightData
{
    float3 Position;
    float3 Color;
    float2 Attenuation;
    float RangeSqr;
    float Range;
};

StructuredBuffer<uint> _LightIndexData;
Texture2D<uint2> _TileData;
StructuredBuffer<DFLightData> _LightData;

inline float remap(float s) 
{
    return (s + 1.0) / 2.0;
}

// Unity
inline float3 DirectBRDF_float(PixelData pd, float3 lightDir)
{
    float3 halfDir = normalize(lightDir + pd.ViewDir);
    float NoH = remap(saturate(dot(pd.Normal, halfDir)));
    float LoH = remap(saturate(dot(lightDir, halfDir)));

    float d = NoH * NoH * pd.Roughness2MinusOne * 1.00001;
    float LoH2 = LoH * LoH;
    float specularTerm = pd.Roughness2 / ((d * d) * max(0.1, LoH2) * pd.NormalizationTerm);

    float3 color = specularTerm * pd.Specular + pd.Diffuse;
    return color;
}

// Unity
inline float3 LightingPhysicallyBased_float(PixelData pd, float attenuation, float3 lightDir, float3 lightCol)
{
    float NdotL = saturate(dot(pd.Normal, lightDir));
    float3 radiance = lightCol * (attenuation * NdotL);
    return DirectBRDF_float(pd, lightDir) * radiance;
}

// Unity
inline float DistanceAttenuation(float distSqr, float2 distAtten) 
{
    float lightAtten = rcp(distSqr);

    float factor = distSqr * distAtten.x;
    float smoothFactor = saturate(1.0 - factor * factor);
    smoothFactor = smoothFactor * smoothFactor;

    return lightAtten * smoothFactor;
}
#endif

void GetAccumulatedLight_float(in float2 pixelPos, in float3 diffuse, in float3 worldPos, in float3 normal, in float3 camPos, in float3 specular, in float roughness, out float4 color)
{
#if SHADERGRAPH_PREVIEW
    color = 0;
#else
    float2 uv = pixelPos / BLOCK_SIZE;
    uint lightStart = _TileData[uv].x;
    uint lightCount = _TileData[uv].y;

    PixelData pd = (PixelData)0;
    pd.Diffuse = diffuse;
    pd.Normal = normal;
    pd.ViewDir = normalize(camPos - worldPos);
    pd.Specular = specular.rgb;
    pd.Roughness = roughness;
    pd.Roughness2 = roughness * roughness;
    pd.Roughness2MinusOne = pd.Roughness2 - 1.0;
    pd.NormalizationTerm = roughness * 4.0 + 2.0;

    float4 col = 0, accLight = 0;
    [loop] for (uint i = 0; i < lightCount; i++)
    {
        uint lightIndex = _LightIndexData[lightStart + i];
        DFLightData ld = _LightData[lightIndex];
         
        float3 ldir = (ld.Position - worldPos);
        float dist = dot(ldir, ldir);
        ldir = normalize(ldir);

        float atten = DistanceAttenuation(dist, ld.Attenuation);

        col.rgb = LightingPhysicallyBased_float(pd, atten, ldir, ld.Color);
        col.a = atten * ld.RangeSqr * 0.1;

        accLight.rgb += col.rgb;
        accLight.a += col.a;
    }

    color = 0;

    float3 lightPos = _MainLightPosition.xyz; 
    float3 lightCol = LightingPhysicallyBased_float(pd, unity_LightData.z, lightPos, _MainLightColor);
    color.rgb += lightCol;
    
    color.rgb += accLight.rgb;
    color.a = accLight.a;
    color.a = saturate(color.a);
#endif
}