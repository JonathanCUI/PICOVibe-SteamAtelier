Shader "SteamWorkshop/PrecisionPBR" {
 Properties { _BaseColor("Color",Color)=(.1,.1,.1,1) _Metallic("Metallic",Range(0,1))=.8 _Smoothness("Smoothness",Range(0,1))=.65 _Finish("Finish",Float)=0 _BaseMap("Base",2D)="white"{} _Cutoff("Cutoff",Float)=.5 }
 SubShader {Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
 Pass {Name "ForwardLit" Tags{"LightMode"="UniversalForward"} Cull Back ZWrite On
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float4 _BaseMap_ST;float _Metallic;float _Smoothness;float _Finish;float _Cutoff;
 CBUFFER_END
 struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 local:TEXCOORD2;float4 shadowCoord:TEXCOORD3;half fog:TEXCOORD4;};
 Varyings vert(Attributes a){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.local=a.positionOS.xyz;o.shadowCoord=GetShadowCoord(p);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
 float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
 half4 frag(Varyings i):SV_Target {
 float grain=hash(floor(i.local*1800));float r=length(i.local.xy);
 float freq=2500;float attenuation=saturate(1-fwidth(r)*freq);
 float lathe=(.5+.5*sin(r*freq*6.283185))*attenuation+(1-attenuation)*.5;
 float finish=_Finish>.5&&_Finish<1.5?lathe:grain;
 SurfaceData surface=(SurfaceData)0;surface.albedo=_BaseColor.rgb*(.965+finish*.07);surface.metallic=_Metallic;surface.smoothness=saturate(_Smoothness+(finish-.5)*.055);surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=1;
 InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=NormalizeNormalPerPixel(i.normalWS);input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);input.shadowCoord=i.shadowCoord;input.fogCoord=i.fog;input.bakedGI=SampleSH(input.normalWS);input.vertexLighting=VertexLighting(input.positionWS,input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
 half4 col=UniversalFragmentPBR(input,surface);col.rgb=MixFog(col.rgb,i.fog);return col;
 }
 ENDHLSL }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}