#ifndef BLOCK_SIZE
#define BLOCK_SIZE 16
#endif

// ### GENERAL DATA ###
float4x4 _MVP;
float4x4 MATRIX_IV;
float4x4 MATRIX_V;
float4x4 MATRIX_IP;
float4 _ProjParams;
float3 _CameraPos;

float _RenderScale;
float2 _InputSize;
float2 _OutputSize;

// ### CULLING/TILE DATA ###
struct Sphere {
    float3 Center;
    float Radius;
};

struct Plane {
    float3 Normal;
    float Distance;
};

struct Frustum {
    Plane planes[4];
};

// ### PIXEL DATA ###
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

RWStructuredBuffer<PixelData> _PixelData; 

// ### LIGHT DATA ###
struct DFLightData
{
    float3 Position;
    float3 Color;
    float2 Attenuation;
    float RangeSqr;
};

StructuredBuffer<DFLightData> _LightData;
uint _LightCount;

// ### HELPER FUNCTIONS ###
inline float3 WorldToViewPos(float3 pos)
{
    return mul(transpose(MATRIX_IV), float4(pos, 1.0)).xyz;
}

inline float WorldTo01Depth(float3 pos)
{
    return -(WorldToViewPos(pos).z * _ProjParams.w);
}

inline float WorldToEyeDepth(float3 pos)
{
    return -(WorldToViewPos(pos).z);
}

inline float4 ComputeNonStereoScreenPos(float4 pos) {
    float4 o = pos * 0.5f;
    o.xy = float2(o.x, o.y*_ProjParams.x) + o.w;
    o.zw = pos.zw;
    return o;
}

inline float4 ClipToView(float4 clip)
{
    float4 view = mul(MATRIX_IP, clip);
    view = view / view.w;
    return view;
}

inline float4 ScreenToView(float4 screenPos)
{
    float2 texCoord = screenPos.xy / _InputSize;
    float4 clip = float4(float2(texCoord.x, 1.0 - texCoord.y) * 2.0 - 1.0, screenPos.z, screenPos.w);
    return ClipToView(clip);
}

inline uint TextureSpaceToArray(uint2 id)
{
    return id.x + _InputSize.x * id.y;
}

inline float remap(float s) 
{
    return (s + 1.0) / 2.0;
}

inline float3 DecodeNormal(float4 enc)
{
    float kScale = 1.7777;
    float3 nn = enc.xyz*float3(2*kScale,2*kScale,0) + float3(-kScale,-kScale,1);
    float g = 2.0 / dot(nn.xyz,nn.xyz);
    float3 n;
    n.xy = g*nn.xy;
    n.z = g-1;
    return n;
}

inline float DecodeFloatRG( float2 enc )
{
    float2 kDecodeDot = float2(1.0, 1/255.0);
    return dot( enc, kDecodeDot );
}

// from: https://www.3dgep.com/forward-plus/
// Check to see if a sphere is fully behind (inside the negative halfspace of) a plane.
// Source: Real-time collision detection, Christer Ericson (2005)
inline bool SphereInsidePlane( Sphere sphere, Plane plane )
{
    return dot( plane.Normal, sphere.Center ) - plane.Distance < -sphere.Radius;
}

// from: https://www.3dgep.com/forward-plus/
// Check to see of a light is partially contained within the frustum.
bool SphereInsideFrustum( Sphere sphere, Frustum frustum, float zNear, float zFar )
{
    bool result = true;
 
    // First check depth
    // Note: Here, the view vector points in the -Z axis so the 
    // far depth value will be approaching -infinity.
    if ( sphere.Center.z - sphere.Radius > zNear || sphere.Center.z + sphere.Radius < zFar )
    {
        result = false;
    }
 
    // Then check frustum planes
    for ( int i = 0; i < 4 && result; i++ )
    {
        if ( SphereInsidePlane( sphere, frustum.planes[i] ) )
        {
            result = false;
        }
    }
 
    return result;
}