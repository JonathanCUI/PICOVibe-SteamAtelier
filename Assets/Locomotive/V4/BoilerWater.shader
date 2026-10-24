Shader "SteamWorkshop/BoilerWater" {
 Properties {_BaseColor("Water",Color)=(.04,.45,.65,.52)}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"} Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};CBUFFER_START(UnityPerMaterial) float4 _BaseColor; CBUFFER_END
 V vert(A a){V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);return o;}
 half4 frag(V i):SV_Target{float3 N=normalize(i.normal);float top=saturate(N.y);float2 w=float2(cos(i.world.x*17+_Time.y*1.7)*.07,sin(i.world.z*21-_Time.y*1.4)*.08);N=normalize(N+float3(w.x,0,w.y)*top);float3 view=normalize(_WorldSpaceCameraPos-i.world);float fres=.025+.975*pow(1-saturate(dot(N,view)),5);float3 reflection=GlossyEnvironmentReflection(reflect(-view,N),i.world,.13,1);Light main=GetMainLight();float spec=pow(saturate(dot(N,normalize(view+main.direction))),180)*1.5;float3 c=lerp(_BaseColor.rgb,reflection,saturate(fres*.55+.16*top))+main.color*spec*.45;float caustic=pow(saturate(sin(i.world.x*23+_Time.y)*sin(i.world.z*19-_Time.y)),6)*.08*top;c+=caustic;return half4(c,saturate(_BaseColor.a+top*.17+fres*.22));}
 ENDHLSL
 }}
}
