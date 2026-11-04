Shader "SwanJourney/XR Sketch Paper"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Pencil study",2D)="white"{}
        [NoScaleOffset] _PaperFibre("Paper fibre relief",2D)="gray"{}
        [NoScaleOffset] _NextTex("Next sheet ink",2D)="white"{}
        _PaperBend("Body motion",Vector)=(0,0,0,0)
        _PaperTail("Free edge motion",Vector)=(0,0,0,0)
        _PageTurn("Turn progress, direction, active",Vector)=(0,0,0,0)
        _PaperReveal("Unrolled amount",Range(0,1))=1
        _SupportHeight("Support height from bottom",Range(0,1))=.40
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            TEXTURE2D(_PaperFibre);SAMPLER(sampler_PaperFibre);
            TEXTURE2D(_NextTex);SAMPLER(sampler_NextTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _PaperBend;
                float4 _PaperTail;
                float4 _PageTurn;
                float _PaperReveal;
                float _SupportHeight;
            CBUFFER_END
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;half3 light:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            // Each spoke bends outwards from the support. Keep its arc length as its direction changes.
            float3 spokeTangent(float h,float2 body,float2 tip,float2 radial,float2 side)
            {
                float2 angle=body*(h*(2-h))+tip*(h*h);
                float2 s,c;sincos(angle,s,c);
                return float3((radial*c.x+side*s.x)*c.y,s.y);
            }
            float3 restingPaper(float2 uv,out float3 paperNormal)
            {
                float3 p=float3(uv.x-.5,(uv.y-.5)*1.5,0);
                float2 fromSupport=float2(p.x,(uv.y-_SupportHeight)*1.5);
                float radius=length(fromSupport);
                float2 radial=radius>.0001?fromSupport/max(radius,.0001):float2(0,1);
                float2 side=float2(-radial.y,radial.x);
                float span=max(0,radius-.075);
                float h=saturate(span/.92);
                // A diagonal grain keeps a push towards the viewer from curling into a uniform bowl.
                float grain=.57+.43*abs(dot(radial,float2(.36,.933)));
                float2 body=float2(-.86*dot(_PaperBend.xy,side),
                    .05-.96*grain*_PaperBend.z-.50*dot(_PaperBend.xy,radial));
                float liftedCorner=smoothstep(.45,1,uv.x)*smoothstep(.55,1,uv.y);
                float2 tip=float2(-.60*dot(_PaperTail.xy,side),
                    .03+.20*liftedCorner-.72*grain*_PaperTail.z-.42*dot(_PaperTail.xy,radial));
                float3 mean=.5*(spokeTangent(h*.211325,body,tip,radial,side)
                    +spokeTangent(h*.788675,body,tip,radial,side));
                p+=(mean-float3(radial,0))*span;
                float3 tangent=spokeTangent(h,body,tip,radial,side);
                float3 across=float3(side,-.22*h*dot(_PaperTail.xy,side));
                paperNormal=normalize(cross(across,tangent));
                // Small asymmetric resting curls, independent of the player's motion.
                float leftFold=saturate((.30-uv.x-uv.y)/.30);
                float rightFold=saturate((.40-(1-uv.x)-uv.y)/.40);
                p.z-=.035*leftFold*leftFold+.065*rightFold*rightFold;
                float2 slope=float2(2*.035*leftFold/.30-2*.065*rightFold/.40,
                    (2*.035*leftFold/.30+2*.065*rightFold/.40)/1.5);
                paperNormal=normalize(paperNormal+float3(slope,0));
                return p;
            }
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 paperNormal;
                float3 p=restingPaper(i.uv,paperNormal);
                if(_PageTurn.z>.5)
                {
                    float t=_PageTurn.x,direction=_PageTurn.y;
                    float wave=sin(PI*t);
                    // The upper edge leads, the lower edge follows. Integrating the
                    // tangent makes a curved ribbon without stretching its width.
                    float rowT=saturate(t+.14*(i.uv.y-.4)*wave);
                    float ease=rowT*rowT*(3-2*rowT);
                    float angle=direction*PI*ease;
                    float curvature=direction*3.8*wave*(.72+.28*i.uv.y);
                    float x=i.uv.x-.5;
                    float localAngle=angle+curvature*x;
                    float sn,cs;sincos(localAngle,sn,cs);
                    float3 curve=float3(x*cos(angle),(i.uv.y-.5)*1.5,-x*sin(angle));
                    if(abs(curvature)>.0001)
                    {
                        curve.x=(sn-sin(angle))/curvature;
                        curve.z=(cs-cos(angle))/curvature;
                    }
                    float3 reverseNormal;
                    float3 reverse=restingPaper(float2(1-i.uv.x,i.uv.y),reverseNormal);
                    float3 base=float3(x,(i.uv.y-.5)*1.5,0);
                    float3 offset=lerp(p-base,float3(-reverse.x,reverse.y,-reverse.z)-base,ease);
                    curve+=float3(cs*offset.x+sn*offset.z,offset.y,-sn*offset.x+cs*offset.z);
                    curve.y-=.045*wave*(4*x*x);
                    paperNormal=normalize(lerp(paperNormal,float3(reverseNormal.x,-reverseNormal.y,reverseNormal.z),ease));
                    paperNormal.xz=float2(cs*paperNormal.x+sn*paperNormal.z,-sn*paperNormal.x+cs*paperNormal.z);
                    p=curve;
                }
                if(_PaperReveal<.9999)
                {
                    // Timing is eased once on the CPU; this maps it to arc length.
                    float opened=saturate((_PaperReveal-.10)/.90);
                    float flatLength=1.5*opened+.08*(i.uv.x-.5)*sin(PI*opened);
                    float rolled=max(0,(1-i.uv.y)*1.5-flatLength);
                    // Archimedean roll: neighbouring wraps have a small separation,
                    // so the paper never occupies the same depth as the layer under it.
                    const float radius=.092,spacing=.003;
                    float a=2*rolled/(radius+sqrt(radius*radius+2*spacing*rolled));
                    float sn,cs;sincos(a,sn,cs);
                    p.y+=rolled-radius*sn-spacing*(a*sn+cs-1);
                    p.z+=radius*(cs-1)+spacing*(a*cs-sn);
                    paperNormal.yz=float2(cs*paperNormal.y-sn*paperNormal.z,sn*paperNormal.y+cs*paperNormal.z);
                    float scale=smoothstep(0,.10,_PaperReveal);
                    p=float3(p.x*scale,.75+(p.y-.75)*scale,p.z*scale);
                }
                i.p.xyz=p;
                o.pos=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;
                // Broad fill keeps graphite legible; the workshop key reveals the curvature.
                float3 key=normalize(float3(-.56,.38,-.72)+.52*TransformWorldToObjectDir(_MainLightPosition.xyz));
                o.light=half3(key.xy,dot(paperNormal,key));
                return o;
            }
            half4 frag(V i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 ink=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;
                bool turningBack=_PageTurn.z>.5 && !IS_FRONT_VFACE(face,true,false);
                if(turningBack)ink=SAMPLE_TEXTURE2D(_NextTex,sampler_NextTex,float2(1-i.uv.x,i.uv.y)).rgb;
                half3 fibre=SAMPLE_TEXTURE2D(_PaperFibre,sampler_PaperFibre,i.uv*float2(2,3)).rgb*2.h-1.h;
                half side=IS_FRONT_VFACE(face,1.h,-1.h);
                half diffuse=.80h+.19h*(side*i.light.z)+.075h*side*dot(fibre.rg,i.light.xy);
                half tooth=1.h+.10h*fibre.b;
                half3 front=ink*half3(1.h,.982h,.946h);
                half3 back=half3(.91h,.875h,.80h)*lerp(.956h,1.h,ink.g);
                return half4((turningBack?front:IS_FRONT_VFACE(face,front,back))*diffuse*tooth,1);
            }
            ENDHLSL
        }
    }
}
