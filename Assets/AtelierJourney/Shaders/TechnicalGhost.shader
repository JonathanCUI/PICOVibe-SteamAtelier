Shader "SteamJourney/TechnicalGhost" {
 Properties { _Tint("Tint",Color)=(.42,.70,.75,1) _Opacity("Opacity",Range(0,1))=.10 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent"}
 Pass { ZWrite Off ZTest LEqual Cull Back Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _Tint;half _Opacity;
 CBUFFER_END
 struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.n);return o;}
 half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);half f=pow(1-saturate(abs(dot(normalize(i.normal),normalize(GetCameraPositionWS()-i.world)))),4);return half4(_Tint.rgb*(.85+.15*f),_Opacity*.32+f*.16);}
 ENDHLSL
 } }
}
