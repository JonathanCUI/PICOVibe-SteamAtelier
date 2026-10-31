Shader "SteamJourney/WhiteSteamV15"{
 Properties{_MainTex("Soft density",2D)="white"{}}
 SubShader{Tags{"Queue"="Transparent+15" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}Pass{Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float _MesaDay;
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float eye:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 wp=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(wp);o.uv=a.uv;o.color=a.color;o.eye=-TransformWorldToView(wp).z;return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float4 d=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);float depth=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);float shade=lerp(.62,1,saturate(d.r*.65+i.uv.y*.45));float3 c=lerp(float3(.39,.50,.68),float3(.95,.97,1),_MesaDay)*shade;return half4(c*i.color.rgb,d.a*i.color.a*saturate((depth-i.eye)*3));}
 ENDHLSL }}
}
