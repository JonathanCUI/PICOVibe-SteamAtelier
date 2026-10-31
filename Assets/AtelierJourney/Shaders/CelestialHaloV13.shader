Shader "SteamJourney/CelestialHaloV13" {
 Properties { [HDR]_Tint("Color",Color)=(1,.7,.3,.5) }
 SubShader {Tags{"Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}Pass{
 Blend SrcAlpha One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)float4 _Tint;CBUFFER_END
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float r=length(i.uv-.5)*2;float a=exp(-r*r*5)*smoothstep(1,.55,r);return half4(_Tint.rgb,_Tint.a*a);}
 ENDHLSL
 }}
}
