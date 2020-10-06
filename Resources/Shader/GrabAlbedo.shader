Shader "Hidden/GrabAlbedo"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}

        Pass
        {
            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            // #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"
// 
            // struct Attributes
            // {
                // float4 positionOS       : POSITION;
                // float2 uv               : TEXCOORD0;
                // UNITY_VERTEX_INPUT_INSTANCE_ID
            // };
// 
            // struct Varyings
            // {
                // float2 uv        : TEXCOORD0;
                // float4 vertex : SV_POSITION;
// 
                // UNITY_VERTEX_INPUT_INSTANCE_ID
                // UNITY_VERTEX_OUTPUT_STEREO
            // };
// 
            // Varyings vert(Attributes input)
            // {
                // Varyings output = (Varyings)0;
// 
                // UNITY_SETUP_INSTANCE_ID(input);
                // UNITY_TRANSFER_INSTANCE_ID(input, output);
                // UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
// 
                // VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                // output.vertex = vertexInput.positionCS;
                // output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
// 
                // return output;
            // }
// 
            // half4 frag(Varyings input) : SV_Target
            // {
                // UNITY_SETUP_INSTANCE_ID(input);
                // UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
// 
                // half2 uv = input.uv;
                // half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                // half3 color = texColor.rgb * _BaseColor.rgb;
                // 
                // return half4(_BaseColor.rgb, 1);
            // }
            ENDHLSL
        }
    }
}
