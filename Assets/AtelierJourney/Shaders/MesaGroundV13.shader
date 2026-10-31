Shader "SteamJourney/MesaGroundV13" {
 Properties { _Control("Weights 0",2D)="red"{} _Control1("Weights 1",2D)="black"{} _Splat0("Sand",2D)="white"{} _Splat1("Fine",2D)="white"{} _Splat2("Gravel",2D)="white"{} _Splat3("Rubble",2D)="white"{} _Splat4("Mud",2D)="white"{} _Normal0("N0",2D)="bump"{} _Normal1("N1",2D)="bump"{} _Normal2("N2",2D)="bump"{} _Normal3("N3",2D)="bump"{} _Normal4("N4",2D)="bump"{} }
 SubShader {Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 TEXTURE2D(_Control);SAMPLER(sampler_Control);TEXTURE2D(_Control1);
 TEXTURE2D(_Splat0);SAMPLER(sampler_Splat0);TEXTURE2D(_Splat1);TEXTURE2D(_Splat2);TEXTURE2D(_Splat3);TEXTURE2D(_Splat4);
 TEXTURE2D(_Normal0);SAMPLER(sampler_Normal0);TEXTURE2D(_Normal1);TEXTURE2D(_Normal2);TEXTURE2D(_Normal3);TEXTURE2D(_Normal4);
 CBUFFER_START(UnityPerMaterial)
 float4 _Splat0_ST,_Splat1_ST,_Splat2_ST,_Splat3_ST,_Splat4_ST;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 wp:TEXCOORD0;float3 n:TEXCOORD1;float3 t:TEXCOORD2;float3 b:TEXCOORD3;float2 uv:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.wp=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.wp);o.n=TransformObjectToWorldNormal(i.n);o.t=TransformObjectToWorldDir(i.t.xyz);o.b=cross(o.n,o.t)*i.t.w;o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);half4 w=SAMPLE_TEXTURE2D(_Control,sampler_Control,i.uv);half w4=SAMPLE_TEXTURE2D(_Control1,sampler_Control,i.uv).r;float2 a=i.uv*_Splat0_ST.xy+_Splat0_ST.zw,b=i.uv*_Splat1_ST.xy+_Splat1_ST.zw,c=i.uv*_Splat2_ST.xy+_Splat2_ST.zw,d=i.uv*_Splat3_ST.xy+_Splat3_ST.zw,e=i.uv*_Splat4_ST.xy+_Splat4_ST.zw;
 half3 color=SAMPLE_TEXTURE2D(_Splat0,sampler_Splat0,a).rgb*w.r+SAMPLE_TEXTURE2D(_Splat1,sampler_Splat0,b).rgb*w.g+SAMPLE_TEXTURE2D(_Splat2,sampler_Splat0,c).rgb*w.b+SAMPLE_TEXTURE2D(_Splat3,sampler_Splat0,d).rgb*w.a+SAMPLE_TEXTURE2D(_Splat4,sampler_Splat0,e).rgb*w4;
 half3 n=UnpackNormal(SAMPLE_TEXTURE2D(_Normal0,sampler_Normal0,a))*w.r+UnpackNormal(SAMPLE_TEXTURE2D(_Normal1,sampler_Normal0,b))*w.g+UnpackNormal(SAMPLE_TEXTURE2D(_Normal2,sampler_Normal0,c))*w.b+UnpackNormal(SAMPLE_TEXTURE2D(_Normal3,sampler_Normal0,d))*w.a+UnpackNormal(SAMPLE_TEXTURE2D(_Normal4,sampler_Normal0,e))*w4;
 n=normalize(n);InputData input=(InputData)0;input.positionWS=i.wp;input.normalWS=normalize(i.t*n.x+i.b*n.y+i.n*n.z);input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.wp);input.shadowCoord=TransformWorldToShadowCoord(i.wp);input.bakedGI=SampleSH(input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);input.shadowMask=1;input.vertexLighting=VertexLighting(i.wp,input.normalWS);SurfaceData s=(SurfaceData)0;s.albedo=color;s.normalTS=n;s.smoothness=.1;s.occlusion=1;s.alpha=1;return UniversalFragmentPBR(input,s);}
 ENDHLSL }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
