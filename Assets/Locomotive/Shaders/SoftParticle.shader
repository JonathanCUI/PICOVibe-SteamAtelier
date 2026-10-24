Shader "SteamWorkshop/SoftParticle" {
 Properties { _Intensity("Intensity",Float)=1 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _Intensity;
 CBUFFER_END
 struct A {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 struct V {float4 position:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
 V vert(A a){V o;o.position=TransformObjectToHClip(a.vertex.xyz);o.color=a.color;o.uv=a.uv;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 half4 frag(V i):SV_Target {float2 p=i.uv*2-1;float n=noise(i.uv*5+_Time.y*.12)*.65+noise(i.uv*11-_Time.y*.13)*.35;float a=saturate(1-dot(p,p));a=a*a*(.45+.65*n);return half4(i.color.rgb*_Intensity,i.color.a*a);}
 ENDHLSL
 }
 }
}
