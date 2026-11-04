Shader "SwanJourney/XR Part Seat"
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
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment SurfaceFragment
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
            float _SwanSeatPulse;
            half4 SurfaceFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 solid; LitPassFragment(input,solid);
                half facing=abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)));
                half rim=pow(saturate(1-facing),3);
                solid.rgb+=half3(1,.73,.32)*rim*_SwanSeatPulse*.85;
                return solid;
            }
            ENDHLSL
        }
    }
}

