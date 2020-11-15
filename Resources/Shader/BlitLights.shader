Shader "Hidden/BlitLights"
{
    Properties { }
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

            sampler2D _DeferredPass_Albedo_Texture;

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _LightsTexture);
                
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // return tex2D(_LightsTexture, i.uv);
                return tex2D(_DeferredPass_Albedo_Texture, i.uv);
                return float4(1,0,1,1);
            }
            ENDHLSL
        }
    }
}
