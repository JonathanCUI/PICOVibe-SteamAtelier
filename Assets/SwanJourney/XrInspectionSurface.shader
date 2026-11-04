Shader "SwanJourney/XR Inspection Surface"
{
    Properties
    {
        _Tint("Tint",Color)=(0.2,0.7,0.9,1)
        _Opacity("Face opacity",Range(0,1))=.02
        _Rim("Grazing opacity",Range(0,1))=.08
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Cull [_Cull] ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Opacity,_Rim;
            CBUFFER_END
            struct A { float4 p:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; half rim:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(i.p.xyz);
                o.p=TransformWorldToHClip(world);
                o.rim=1-saturate(abs(dot(TransformObjectToWorldNormal(i.normal),GetWorldSpaceNormalizeViewDir(world))));
                return o;
            }
            half4 frag(V i):SV_Target
            {
                half a=saturate(_Opacity+_Rim*i.rim*i.rim);
                return half4(_Tint.rgb*a,a);
            }
            ENDHLSL
        }
    }
}
