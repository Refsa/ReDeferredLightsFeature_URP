Shader "Hidden/DebugGBuffer"
{
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            uint _DebugMode;
            float4x4 MATRIX_IV;
            uint _LightCount;

            sampler2D _MainTex;
            float4 _MainTex_ST;

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
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            static const float TileStrengthScale = 256.0;

            inline float DecodeFloatRG( float2 enc )
            {
                float2 kDecodeDot = float2(1.0, 1/255.0);
                return dot( enc, kDecodeDot );
            }
            inline float3 DecodeViewNormalStereo( float4 enc4 )
            {
                float kScale = 1.7777;
                float3 nn = enc4.xyz*float3(2*kScale,2*kScale,0) + float3(-kScale,-kScale,1);
                float g = 2.0 / dot(nn.xyz,nn.xyz);
                float3 n;
                n.xy = g*nn.xy;
                n.z = g-1;
                return n;
            }
            inline void DecodeDepthNormal( float4 enc, out float depth, out float3 normal )
            {
                depth = DecodeFloatRG (enc.zw);
                normal = DecodeViewNormalStereo (enc);
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 dnenc = tex2D(_DeferredPass_DepthNormals_Texture, i.uv);
                float3 normal = 0; float depth = 0; 
                DecodeDepthNormal(dnenc, depth, normal);

                float4 specSmooth = tex2D(_DeferredPass_Specular_Texture, i.uv);

                if (_DebugMode == 0) return tex2D(_MainTex, i.uv);
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
                    float4 bb = tex2D(_MainTex, i.uv);

                    return float4(lerp(bb.rgb, strength, 0.5), 1.0);
                }

                return float4(0,0,0,1);
            }
            ENDHLSL
        }
    }
}
