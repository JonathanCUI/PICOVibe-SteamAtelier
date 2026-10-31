Shader "SteamJourney/SoftDesertDustV14"{
 Properties{_MainTex("Dust feather",2D)="white"{}}
 SubShader{Tags{"Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}Pass{
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float _MesaDay;
 struct A{float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;float eye:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 wp=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(wp);o.eye=-TransformWorldToView(wp).z;o.uv=i.uv;o.c=i.c;return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);half a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;float depth=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);float fade=saturate((depth-i.eye)/.55);return half4(i.c.rgb*lerp(float3(.19,.25,.38),float3(1,1,1),_MesaDay),i.c.a*a*fade);}
 ENDHLSL
 }}
}
