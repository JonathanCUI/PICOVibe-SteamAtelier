Shader "SteamAtelier/XR Passthrough Hand"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-100" "RenderType"="Opaque" }
        Cull Off
        ZTest LEqual
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        struct Attributes { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 position : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            // A small inward margin keeps the mask just inside the tracked silhouette.
            float3 world = TransformObjectToWorld(input.vertex.xyz);
            output.position = TransformWorldToHClip(world - TransformObjectToWorldNormal(input.normal) * .0015);
            return output;
        }
        half4 Frag(Varyings input) : SV_Target { return 0; }
        ENDHLSL
        // Leave transparent camera pixels at tracked-hand depth for the passthrough underlay.
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
