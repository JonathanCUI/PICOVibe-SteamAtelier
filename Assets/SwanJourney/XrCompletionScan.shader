Shader "SwanJourney/XR Completion Scan"
{
    Properties
    {
        _BaseMap("Albedo",2D)="white" {}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _BumpMap("Normals",2D)="bump" {}
        _BumpScale("Normal scale",Float)=1
        _MetallicGlossMap("Metal and gloss",2D)="white" {}
        _Metallic("Metal",Float)=0
        _Smoothness("Smoothness",Float)=.5
        _OcclusionMap("Occlusion",2D)="white" {}
        _OcclusionStrength("Occlusion strength",Float)=1
        _EmissionMap("Emission",2D)="white" {}
        _EmissionColor("Emission color",Color)=(0,0,0,1)
        _Surface("Surface",Float)=0
        _Cull("Cull",Float)=2
        _SpecularHighlights("Specular",Float)=1
        _EnvironmentReflections("Reflections",Float)=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment CompletionFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile_local _ _METALLICSPECGLOSSMAP
            #pragma multi_compile_local _ _SPECULARHIGHLIGHTS_OFF
            #pragma multi_compile_local _ _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #define _OCCLUSIONMAP 1
            #define _EMISSION 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #ifndef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #endif
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            float4x4 _SwanFinishToLocal;
            float4 _SwanFinishRange,_SwanFinishClock;
            half4 CompletionFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 solid;
                LitPassFragment(input,solid);
                float seconds=_SwanFinishClock.x;
                float envelope=smoothstep(0,.28,seconds)*(1-smoothstep(1.55,2.05,seconds));
                float length=max(.01,_SwanFinishRange.z);
                float3 local=mul(_SwanFinishToLocal,float4(input.positionWS,1)).xyz;
                float direction=sign(_SwanFinishRange.y-_SwanFinishRange.x);
                float travel=smoothstep(.28,1.55,seconds);
                float front=lerp(_SwanFinishRange.x-direction*length*.055,
                    _SwanFinishRange.y+direction*length*.06,travel);
                float behind=(front-local.x)*direction/length;
                float band=1-smoothstep(.002,.010,abs(behind));
                float tail=smoothstep(-.003,.004,behind)*(1-smoothstep(.025,.115,behind));
                float3 n=abs(mul((float3x3)_SwanFinishToLocal,input.normalWS));
                float3 p=local/(length*.008);
                float2 uv=n.y>max(n.x,n.z)?p.xz:(n.x>n.z?p.zy:p.xy);
                float2 edge=min(frac(uv),1-frac(uv));
                float2 gridStroke=1-smoothstep(.025,.065+min(fwidth(uv),.30),edge);
                float grid=max(gridStroke.x,gridStroke.y);
                float facing=abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float rim=pow(saturate(1-facing),3.5);
                float highlight=envelope*max(rim*1.12,max(band*1.55,tail*(.15+grid*.88)));
                half3 gold=half3(1,.68,.20);
                half3 core=half3(1,.94,.62);
                // Local emission gives a luminous edge without HDR bloom or an extra render pass.
                solid.rgb+=lerp(gold,core,saturate(band*.85+rim*.25))*highlight;
                return solid;
            }
            ENDHLSL
        }
    }
}
