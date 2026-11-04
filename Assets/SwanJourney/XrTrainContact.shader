Shader "SwanJourney/XR Train Contact"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-25" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            Blend DstColor Zero, Zero One
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=abs(i.uv*2-1);
                float alpha=(1-smoothstep(.42,1,p.x))*(1-smoothstep(.13,1,p.y));
                return half4(1-alpha*.32,1-alpha*.30,1-alpha*.27,1);
            }
            ENDHLSL
        }
    }
}
