Shader "Hidden/WorldPosition"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Back
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return float4(i.worldPos, 1.0);

                // float3 cameraDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos.xyz);
                // return float4(cameraDir, 0.0);
            }
            ENDCG
        }
    }
}
