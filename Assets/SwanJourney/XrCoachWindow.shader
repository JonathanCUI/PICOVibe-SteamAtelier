Shader "SwanJourney/XR Coach Window"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half _SwanSkyNight;
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos:SV_POSITION; half3 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=TransformObjectToHClip(i.positionOS.xyz);
                half h=saturate(i.uv.y);
                half3 glass=lerp(half3(.023,.020,.016),half3(.052,.061,.063),h);
                half3 warm=lerp(half3(.59,.235,.064),half3(.96,.63,.27),h);
                o.color=lerp(glass,warm,smoothstep(.22,.88,_SwanSkyNight));
                return o;
            }
            half4 frag(V i):SV_Target {return half4(i.color,1);}
            ENDHLSL
        }
    }
}
