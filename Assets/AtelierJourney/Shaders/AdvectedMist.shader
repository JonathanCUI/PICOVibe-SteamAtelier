Shader "SteamJourney/AdvectedMist" {
Properties {_Tint("Tint",Color)=(1,1,1,1)}
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent"}
Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float4 _Tint;float _JourneyTime;
struct A{float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.c=a.c*_Tint;o.uv=a.uv;return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
half4 frag(V i):SV_Target{float2 p=i.uv*2-1;float t=_JourneyTime;float2 warp=float2(noise(i.uv*5+t*.11),noise(i.uv*5-t*.09))-.5;float n=noise(i.uv*5+warp*.7-float2(t*.04,t*.13))*.57+noise(i.uv*13+warp-t*.08)*.29+noise(i.uv*28)*.14;float shape=saturate((1-dot(p,p))*.95+(n-.5)*.48);float density=pow(shape,1.8)*smoothstep(.14,.78,n);float rim=pow(saturate(1-abs(p.x+p.y*.2)),2);return half4(i.c.rgb*(.72+n*.65+rim*.18),density*i.c.a);}
ENDHLSL}}}
