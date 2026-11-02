Shader "SwanJourney/XR Valley Cloud"
{
    Properties
    {
        _Cloud("Baked V16 density and daylight",3D)="black"{}
        _Seed("Authored shape seed",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Front ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE3D(_Cloud); SAMPLER(sampler_Cloud);
            half _SwanSkyNight;
            CBUFFER_START(UnityPerMaterial)
                float _Seed;
            CBUFFER_END
            struct A {float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 pos:SV_POSITION; float3 objectPos:TEXCOORD0; half2 drift:TEXCOORD1;
                      UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=TransformObjectToHClip(i.p.xyz);o.objectPos=i.p.xyz;
                o.drift=sin(_Time.y*half2(.26,.33)+_Seed+half2(0,1.4))*.032;
                return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 eye=TransformWorldToObject(GetCameraPositionWS());
                float3 ray=normalize(i.objectPos-eye);
                float3 inv=rcp(ray+1e-7);
                float3 front=min((-.5-eye)*inv,(.5-eye)*inv);
                float nearT=max(0,max(front.x,max(front.y,front.z)));
                float rayLength=length(i.objectPos-eye)-nearT;
                if(rayLength<=0) discard;
                const int steps=16;
                float dt=rayLength/steps;
                float3 delta=ray*dt,uv=eye+ray*(nearT+.5*dt)+.5;
                half4 sum=0;
                half3 shade=lerp(half3(.24,.30,.39),half3(.13,.20,.33),_SwanSkyNight);
                half3 bright=lerp(half3(1.03,1.01,.96),half3(.59,.72,.91),_SwanSkyNight);
                [loop] for(int j=0;j<steps;j++,uv+=delta)
                {
                    float3 q=uv;
                    q.xz+=i.drift*(uv.y-.35);
                    half2 v=SAMPLE_TEXTURE3D_LOD(_Cloud,sampler_Cloud,q,0).rg;
                    half opacity=1-exp2(-27.412*v.r*dt);
                    half3 cloud=lerp(shade,bright,v.g);
                    half weight=(1-sum.a)*opacity;
                    sum.rgb+=cloud*weight;sum.a+=weight;
                    if(sum.a>.985h) break;
                }
                return sum;
            }
            ENDHLSL
        }
    }
}
