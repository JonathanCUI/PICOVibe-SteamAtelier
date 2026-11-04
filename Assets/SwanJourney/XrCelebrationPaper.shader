Shader "SwanJourney/XR Celebration Paper"
{
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SwanFinishClock;
            CBUFFER_START(UnityPerMaterial)
            float4 _PaperSize,_PaperCentre;
            CBUFFER_END
            struct Attributes
            {
                float3 position:POSITION; float3 normal:NORMAL; half4 color:COLOR;
                float2 uv:TEXCOORD0; float4 launch:TEXCOORD1; float4 velocity:TEXCOORD2; float4 motion:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position:SV_POSITION; half3 color:COLOR; float alive:TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float3 Rotate(float3 p,float2 angle)
            {
                float sx,cx,sy,cy;sincos(angle.x,sx,cx);sincos(angle.y,sy,cy);
                p.yz=float2(cx*p.y-sx*p.z,sx*p.y+cx*p.z);
                p.xz=float2(cy*p.x+sy*p.z,-sy*p.x+cy*p.z);return p;
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float age=_SwanFinishClock.x-v.launch.w;
                float a=max(0,age),drift=(1-exp(-a*1.2))/1.2;
                float3 centre=_PaperCentre.xyz+v.launch.xyz*_PaperSize.xyz;
                centre+=float3(v.velocity.x*drift,v.velocity.y*a-.68*a*a,v.velocity.z*drift)*_PaperSize.xyz;
                centre.x+=sin(a*3.0+v.velocity.w)*a*.013*_PaperSize.x;
                centre.z+=sin(a*2.3+v.motion.z)*a*.08*_PaperSize.z;
                float2 angle=float2(v.velocity.w+a*v.motion.x,v.motion.z+a*v.motion.y);
                float3 paper=v.position*_PaperSize.x;
                paper.z+=sin(a*5+v.uv.x*6.28+v.velocity.w)*.003*_PaperSize.x*(.2+v.uv.x)*v.motion.w;
                float3 normal=Rotate(v.normal,angle);
                paper=Rotate(paper,angle);
                o.position=TransformObjectToHClip(centre+paper);
                // Small fold shading, but no dark back faces as the coloured paper tumbles.
                half light=.93+.07*abs(dot(normal,normalize(float3(.3,.8,-.5))));
                o.color=v.color.rgb*light;
                o.alive=step(0,age)*smoothstep(0,.10,a)*(1-smoothstep(3.05,3.85,_SwanFinishClock.x));
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Opaque paper with screen-door fading: no transparent sorting or full-screen pass.
                float threshold=frac(52.9829189*frac(dot(floor(i.position.xy),float2(.06711056,.00583715))));
                clip(i.alive-threshold-.001);
                return half4(i.color,1);
            }
            ENDHLSL
        }
    }
}
