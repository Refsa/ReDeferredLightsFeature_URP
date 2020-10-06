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

            sampler2D _BackBuffer_Image;
            float4 _BackBuffer_Image_ST;

            sampler2D _DeferredPass_DepthNormals_Texture;
            float4 _DeferredPass_DepthNormals_Texture_ST;

            sampler2D _DeferredPass_WorldPosition_Texture;
            float4 _DeferredPass_WorldPosition_Texture_ST;

            sampler2D _DeferredPass_Albedo_Texture;
            float4 _DeferredPass_Albedo_Texture_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BackBuffer_Image);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 dnenc = tex2D(_DeferredPass_DepthNormals_Texture, i.uv);
                float3 normal = 0; float depth = 0;
                DecodeDepthNormal(dnenc, depth, normal);

                if (_DebugMode == 0) return tex2D(_BackBuffer_Image, i.uv);
                else if (_DebugMode == 1) return float4(normal, 1.0);
                else if (_DebugMode == 2) return float4(depth, depth, depth, 1.0);
                else if (_DebugMode == 3) return tex2D(_DeferredPass_WorldPosition_Texture, i.uv);
                else if (_DebugMode == 4) return tex2D(_DeferredPass_Albedo_Texture, i.uv);

                return float4(0,0,0,1);
            }
            ENDCG
        }
    }
}
