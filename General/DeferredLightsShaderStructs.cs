using UnityEngine;

struct Plane
{
    public Vector3 Normal;
    public float Distance;
}

struct Frustum
{
    public Plane NearPlane; // Close Plane
    public Plane FarPlane; // Far Plane
    public Plane LeftPlane; // Left
    public Plane RightPlane; // Right
    public Plane UpPlane; // Up
    public Plane DownPlane; // Down
}

public struct LightData
{
    public Vector3 Position;
    public Vector3 Color;
    public Vector2 Attenuation;
    public float RangeSqr;
}

public struct PixelData
{
    public Vector3 Diffuse;
    public Vector3 Normal;
    public float Depth;
    public Vector3 ViewDir;
    public Vector3 Position;
    public Vector3 Specular;
    public float Roughness;
    public float Roughness2;
    public float Roughness2MinusOne;
    public float NormalizationTerm;
};