Shader "Hidden/DebugGBuffer"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            uint _DebugMode;
            float4x4 MATRIX_IV;
            uint _LightCount;

            sampler2D _BackBuffer_Image;
            float4 _BackBuffer_Image_ST;

            sampler2D _DepthTexture;

            sampler2D _DeferredPass_DepthNormals_Texture;
            float4 _DeferredPass_DepthNormals_Texture_ST;

            sampler2D _DeferredPass_WorldPosition_Texture;
            float4 _DeferredPass_WorldPosition_Texture_ST;

            sampler2D _DeferredPass_Albedo_Texture;
            float4 _DeferredPass_Albedo_Texture_ST;

            sampler2D _DeferredPass_Specular_Texture;
            float4 _DeferredPass_Specular_Texture_ST;

            Texture2D<uint2> _TileData;
            sampler2D sampler_TileData;

            sampler2D _HeatmapTexture;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BackBuffer_Image);
                return o;
            }

            static const float TileStrengthScale = 256.0;

            float4 frag (v2f i) : SV_Target
            {
                float4 dnenc = tex2D(_DeferredPass_DepthNormals_Texture, i.uv);
                float3 normal = 0; float depth = 0; 
                DecodeDepthNormal(dnenc, depth, normal);
                // depth = tex2D(_DepthTexture, i.uv);

                float4 specSmooth = tex2D(_DeferredPass_Specular_Texture, i.uv);

                if (_DebugMode == 0) return tex2D(_BackBuffer_Image, i.uv);
                else if (_DebugMode == 1) return float4(normal, 1.0);
                else if (_DebugMode == 6) return float4(mul(MATRIX_IV, float4(normal, 0)).xyz, 1.0);
                else if (_DebugMode == 2) return float4(depth, depth, depth, 1.0);
                else if (_DebugMode == 3) return tex2D(_DeferredPass_WorldPosition_Texture, i.uv);
                else if (_DebugMode == 4) return tex2D(_DeferredPass_Albedo_Texture, i.uv);
                else if (_DebugMode == 5) return float4(specSmooth.rgb, 1.0);
                else if (_DebugMode == 7) return float4(specSmooth.a, specSmooth.a, specSmooth.a, 1.0);
                else if (_DebugMode == 8)
                {   
                    uint2 tileData = _TileData.Load(float3(i.uv * _ScreenParams.xy * rcp(16), 0));
                    float strength = tileData.y / TileStrengthScale;

                    float3 color = tex2D(_HeatmapTexture, float2(strength, 0.5)).rgb;

                    return float4(color * 0.25, 1.0);
                }
                else if (_DebugMode == 9)
                {
                    uint2 tileData = _TileData.Load(float3(i.uv * _ScreenParams.xy * rcp(16), 0));
                    float strength = tileData.y / TileStrengthScale;
                    float4 bb = tex2D(_BackBuffer_Image, i.uv);

                    return float4(lerp(bb.rgb, strength, 0.05), 1.0);
                }

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}
