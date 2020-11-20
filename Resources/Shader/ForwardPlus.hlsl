#ifndef BLOCK_SIZE
#define BLOCK_SIZE 16
#endif

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

// Unity
inline float3 DirectBRDF_float(PixelData pd, float3 lightDir)
{
    float3 halfDir = normalize(lightDir + pd.ViewDir);
    float NoH = saturate(dot(pd.Normal, halfDir));
    float LoH = saturate(dot(lightDir, halfDir));

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

void GetAccumulatedLight_float(in float2 pixelPos, in float3 worldPos, out float4 color)
{
    float2 uv = pixelPos / BLOCK_SIZE;
    uint lightStart = _TileData[uv].x;
    uint lightCount = _TileData[uv].y;

    float4 col = 0, accLight = 0;
    [loop] for (uint i = 0; i < lightCount; i++)
    {
        uint lightIndex = _LightIndexData[lightStart + i];
        DFLightData ld = _LightData[lightIndex];
         
        float3 ldir = (ld.Position - worldPos);
        float dist = dot(ldir, ldir);

        float atten = DistanceAttenuation(dist, ld.Attenuation);

        col.rgb = ld.Color;
        col.a = atten * ld.RangeSqr * 0.1;

        accLight.rgb += col.rgb * col.a;
        accLight.a += col.a;
    }
    accLight.a = saturate(accLight.a);

    color.rgb = accLight.rgb * accLight.a;
    color.a = 1.0;
}