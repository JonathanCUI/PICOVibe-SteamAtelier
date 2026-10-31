Shader "SteamJourney/LampStarV15"{
 Properties{_Tint("Glow",Color)=(1,.75,.4,1)}
 SubShader{Tags{"Queue"="Transparent+30" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}Pass{Blend SrcAlpha One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial) float4 _Tint; CBUFFER_END
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{float2 p=i.uv*2-1;float r=length(p);float rays=exp(-abs(p.x+p.y)*85)+exp(-abs(p.x-p.y)*85);float alpha=(exp(-r*r*40)*.9+rays*.32*pow(saturate(1-r),2))*smoothstep(1,.75,r);return half4(_Tint.rgb*2,alpha*_Tint.a);}
 ENDHLSL }}
}
