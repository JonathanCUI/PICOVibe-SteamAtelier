Shader "SteamJourney/StudioSoftMist"
{
 Properties { _Tint("Tint",Color)=(1,1,1,1) _SoftDistance("Contact fade",Range(.01,1))=.22 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   float _SoftDistance;
   CBUFFER_END
   float _JourneyTime;
   struct A {float4 p:POSITION;half4 c:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 p:SV_POSITION;half4 c:COLOR;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;half fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A a) {V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.uv=a.uv;o.c=a.c*_Tint;o.fog=ComputeFogFactor(o.p.z);return o;}
   float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p) {float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   half4 frag(V i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=i.uv*2-1;
    float t=_JourneyTime*.08;
    float n=noise(i.uv*4.5+float2(t,-t))*.65+noise(i.uv*11.0-t*.7)*.35;
    float shape=saturate(1-dot(p,p)+(n-.5)*.42);
    float density=shape*shape*smoothstep(.12,.76,n);
    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.p));
    float eye=LinearEyeDepth(raw,_ZBufferParams);
    float part=-TransformWorldToView(i.world).z;
    float soft=saturate((eye-part)/max(.01,_SoftDistance));
    float nearFade=saturate((part-.12)*2);
    Light key=GetMainLight();
    half3 illumination=clamp(SampleSH(half3(0,1,0))*.7+key.color*.20,half3(.55,.55,.55),half3(1.3,1.3,1.3));
    half3 color=i.c.rgb*illumination*(.80+n*.35);
    return half4(MixFog(color,i.fog),saturate(density*i.c.a*soft*nearFade));
   }
   ENDHLSL
  }
 }
}
