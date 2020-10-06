Shader "Hidden/GrabAlbedo"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            uniform float4 _BaseMap_ST;

            v2f vert (appdata_base  v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
                return o;
            }

            uniform sampler2D _BaseMap;

            float4 frag (v2f i) : SV_Target
            {
                return float4(1,0,0,1);

                float4 col = tex2D(_BaseMap, i.uv);
                return col;
            }
            ENDHLSL
        }
    }
}
