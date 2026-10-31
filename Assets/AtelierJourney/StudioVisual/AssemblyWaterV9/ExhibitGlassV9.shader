Shader "SteamJourney/V9ExhibitGlass" {
Properties{_Tint("Glass tint",Color)=(.62,.79,.83,.055) _Reflection("Reflection",Range(0,2))=.85 _Roughness("Roughness",Range(0,1))=.08}
SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+30"}
Pass{Tags{"LightMode"="UniversalForward"} Blend One OneMinusSrcAlpha ZWrite Off Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _Tint;float _Reflection,_Roughness;
CBUFFER_END
struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
half4 frag(V i):SV_Target{float3 N=normalize(i.n),V=GetWorldSpaceNormalizeViewDir(i.w);float nv=saturate(dot(N,V));float fres=.055+.945*pow(1-nv,5);float3 reflection=GlossyEnvironmentReflection(reflect(-V,N),i.w,_Roughness,1);
Light l=GetMainLight();float spec=pow(saturate(dot(N,normalize(V+l.direction))),240);float alpha=saturate(_Tint.a+fres*.75);return half4(_Tint.rgb*_Tint.a+reflection*fres*_Reflection+l.color*spec*.55,alpha);}
ENDHLSL}}}
