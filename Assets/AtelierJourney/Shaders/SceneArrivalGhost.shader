Shader "SteamJourney/SceneArrivalGhost" {
 Properties{_Wire("Structural edges",Float)=0}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15"} Pass{Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _ArrivalRange;float _Wire;float4x4 _ArrivalWorldToLocal;
 struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 scene=mul(_ArrivalWorldToLocal,float4(i.w,1)).xyz;float u=(scene.y-_ArrivalRange.x)/_ArrivalRange.y;float scan=lerp(-.04,1.04,_ArrivalRange.z);float d=u-scan;clip(d+.003);
  float band=1-smoothstep(.002,.025,abs(d));float trail=(1-smoothstep(.025,.15,d))*step(0,d);
  float rim=pow(1-saturate(abs(dot(normalize(i.n+1e-5),GetWorldSpaceNormalizeViewDir(i.w)))),2);
  float spacing=max(.06,_ArrivalRange.w/110);float3 grid=abs(frac(scene/spacing)-.5)/max(fwidth(scene/spacing),.001);float etched=1-saturate(min(grid.x,min(grid.y,grid.z)));
  float alpha=lerp(.012+rim*.075+etched*.028,.27,_Wire)+band*lerp(.58,.65,_Wire)+trail*.12;
  half3 color=lerp(half3(.025,.35,.85),half3(.34,.95,1),band*.95+trail*.13);return half4(color,saturate(alpha));}
 ENDHLSL}}}
