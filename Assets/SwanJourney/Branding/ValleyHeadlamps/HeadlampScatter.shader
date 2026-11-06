Shader "SwanJourney/XR Headlamp Scatter"
{
    Properties{_Density("Light scatter",Range(0,1))=.22}
    SubShader
    {
        Tags{"Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Density;
            CBUFFER_END
            struct A{float4 p:POSITION;float3 axis:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 pos:SV_POSITION;half2 uv:TEXCOORD0;half facing:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 eye=TransformWorldToObject(GetCameraPositionWS());
                o.uv=i.uv;
                o.facing=smoothstep(.15,.99,normalize(eye).z);
                if(i.uv.y>1.5)
                {
                    float3 centre=TransformWorldToView(TransformObjectToWorld(float3(0,0,.045)));
                    float scale=length(mul((float3x3)GetObjectToWorldMatrix(),float3(1,0,0)));
                    float2 corner=float2(i.uv.x,i.uv.y-2)*2-1;
                    centre.xy+=corner*scale*lerp(.42,1.28,o.facing);
                    o.pos=mul(UNITY_MATRIX_P,float4(centre,1));
                    return o;
                }
                float3 across=cross(i.axis,eye);
                across*=rsqrt(max(dot(across,across),1e-8));
                float3 p=float3(0,i.p.yz)+across*i.p.x;
                o.pos=TransformObjectToHClip(p);o.uv=i.uv;return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if(i.uv.y>1.5h)
                {
                    half2 p=(i.uv-half2(0,2))*2-1;
                    half r=length(p);
                    half fade=1-smoothstep(.68h,1.h,r);
                    half halo=exp2(-7.h*r)*fade;
                    // Three crossing, feathered streaks around the white-hot lens.
                    half rayDistance=min(abs(p.x), min(abs(dot(p,half2(.5h,.866h))),abs(dot(p,half2(.5h,-.866h)))));
                    half star=exp2(-94.h*rayDistance-4.6h*r)*fade;
                    half core=1-smoothstep(.025h,.155h,r);
                    half strength=saturate(_Density*5.h)*(.05h+.95h*i.facing);
                    half a=saturate(1.6h*halo+.7h*star+core)*strength;
                    half3 tint=lerp(half3(1,.34h,.075h),half3(1,.96h,.8h),saturate(core+.65h*star));
                    return half4(tint*a,a);
                }
                half x=abs(i.uv.x*2-1);
                half edge=exp2(-5.h*x*x)*(1-smoothstep(.5h,1.h,x));
                half falloff=exp2(-4.h*i.uv.y)*(1-smoothstep(.32h,.92h,i.uv.y));
                // A faint scattering shaft, not an opaque triangular light card.
                // Actual illumination is provided by the existing spot lights.
                half a=.55h*_Density*edge*falloff*smoothstep(0.h,.045h,i.uv.y);
                return half4(half3(1,.83,.60)*a,a);
            }
            ENDHLSL
        }
    }
}
