Shader "Hidden/BlitLights"
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

            sampler2D _LightsTexture;
            float4 _LightsTexture_ST;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _DeferredPass_Albedo_Texture;
            sampler2D _DeferredPass_DepthNormals_Texture;

            sampler2D _GrabTexture_AfterOpaques;
            float4 _GrabTexture_AfterOpaques_ST;

            sampler2D _DepthTexture_AfterOpaques;

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _GrabTexture_AfterOpaques);
                // o.uv = v.uv;
                
                return o;
            }

            inline float DecodeFloatRG( float2 enc )
            {
                float2 kDecodeDot = float2(1.0, 1 / 255.0);
                return dot( enc, kDecodeDot );
            }

            inline float Linear01Depth( float z )
            {
                return 1.0 / (_ZBufferParams.x * z + _ZBufferParams.y);
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 lightSize = float2(2048, 2048);
                float2 sizediff = _ScreenParams.xy / lightSize;

                float2 luv = i.uv * sizediff;
                float4 light = tex2D(_LightsTexture, luv);

                float4 scene = tex2D(_GrabTexture_AfterOpaques, i.uv);

                float sceneDepth = tex2D(_DepthTexture_AfterOpaques, i.uv).r;
                sceneDepth = Linear01Depth(sceneDepth);

                float4 defEnc = tex2D(_DeferredPass_DepthNormals_Texture, i.uv);
                float defDepth = DecodeFloatRG(defEnc.zw);

                float diff = abs(sceneDepth - defDepth);
                // return diff > 0.0002;

                if (diff > 0.0002 && sceneDepth > 0 && sceneDepth < 1){
                    return scene;
                } else {
                    float3 lightStrength = light.rgb * light.a;
                    scene.rgb += lightStrength;
                    return scene;
                }

            }
            ENDHLSL
        }
    }
}
