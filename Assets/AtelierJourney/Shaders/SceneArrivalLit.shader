Shader "SteamJourney/SceneArrivalLit" {
 Properties {
  _BaseMap("Base",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1)
  _Metallic("Metallic",Range(0,1))=0 _Smoothness("Smoothness",Range(0,1))=.5
  _MetallicGlossMap("Metallic smoothness",2D)="white"{} _BumpMap("Normal",2D)="bump"{} _BumpScale("Normal scale",Float)=1
  _OcclusionMap("Occlusion",2D)="white"{} _OcclusionStrength("Occlusion strength",Float)=1
  _EmissionMap("Emission",2D)="white"{} _EmissionColor("Emission color",Color)=(0,0,0,0)
  _HasNormal("Has normal",Float)=0 _HasMetalMap("Has metallic",Float)=0 _HasOcclusion("Has occlusion",Float)=0 _HasEmission("Has emission",Float)=0 _Cull("Cull",Float)=2
 }
 SubShader {Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
 float4 _ArrivalRange;float4x4 _ArrivalWorldToLocal;
 float3 ArrivalPosition(float3 world){return mul(_ArrivalWorldToLocal,float4(world,1)).xyz;}
 void ArrivalClip(float3 world){float u=(ArrivalPosition(world).y-_ArrivalRange.x)/_ArrivalRange.y;clip(lerp(-.04,1.04,_ArrivalRange.z)-u);}
 ENDHLSL
 Pass {Name "ForwardLit" Tags{"LightMode"="UniversalForward"} ZWrite On Cull[_Cull]
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile _ _ADDITIONAL_LIGHTS
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);TEXTURE2D(_MetallicGlossMap);SAMPLER(sampler_MetallicGlossMap);TEXTURE2D(_OcclusionMap);SAMPLER(sampler_OcclusionMap);TEXTURE2D(_EmissionMap);SAMPLER(sampler_EmissionMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST,_BaseColor,_EmissionColor;float _Metallic,_Smoothness,_BumpScale,_OcclusionStrength,_HasNormal,_HasMetalMap,_HasOcclusion,_HasEmission,_Cull;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float4 t:TEXCOORD2;float2 uv:TEXCOORD3;float fog:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.t=float4(TransformObjectToWorldDir(a.t.xyz),a.t.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);ArrivalClip(i.w);
  half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;half4 metal=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,i.uv);
  half3 n=normalize(i.n);if(_HasNormal>.5){half3 tangent=normalize(i.t.xyz);half3 bitangent=cross(n,tangent)*i.t.w;half3 map=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);n=normalize(tangent*map.x+bitangent*map.y+n*map.z);}
  SurfaceData s=(SurfaceData)0;s.albedo=base.rgb;s.metallic=lerp(_Metallic,metal.r,_HasMetalMap);s.smoothness=_Smoothness*lerp(1,metal.a,_HasMetalMap);s.alpha=1;s.normalTS=half3(0,0,1);s.occlusion=lerp(1,SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,i.uv).g,_HasOcclusion*_OcclusionStrength);s.emission=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb*_HasEmission;
  InputData data=(InputData)0;data.positionWS=i.w;data.normalWS=n;data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);data.shadowCoord=TransformWorldToShadowCoord(i.w);data.bakedGI=SampleSH(n);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);data.shadowMask=half4(1,1,1,1);
  half4 color=UniversalFragmentPBR(data,s);float u=(ArrivalPosition(i.w).y-_ArrivalRange.x)/_ArrivalRange.y;float edge=1-smoothstep(0,.013,lerp(-.04,1.04,_ArrivalRange.z)-u);color.rgb+=half3(.02,.52,.85)*edge;color.rgb=MixFog(color.rgb,i.fog);return color;
 }
 ENDHLSL}
 Pass {Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0 Cull[_Cull]
 HLSLPROGRAM
 #pragma vertex vertShadow
 #pragma fragment fragShadow
 #pragma multi_compile_instancing
 #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
 float3 _LightDirection,_LightPosition;
 struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;};
 V vertShadow(A a){V o;UNITY_SETUP_INSTANCE_ID(a);o.w=TransformObjectToWorld(a.p.xyz);float3 n=TransformObjectToWorldNormal(a.n);
 #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
 float3 light=normalize(_LightPosition-o.w);
 #else
 float3 light=_LightDirection;
 #endif
 o.p=TransformWorldToHClip(ApplyShadowBias(o.w,n,light));
 #if UNITY_REVERSED_Z
 o.p.z=min(o.p.z,UNITY_NEAR_CLIP_VALUE);
 #else
 o.p.z=max(o.p.z,UNITY_NEAR_CLIP_VALUE);
 #endif
 return o;}
 half4 fragShadow(V i):SV_Target{ArrivalClip(i.w);return 0;}
 ENDHLSL}
 }
}
