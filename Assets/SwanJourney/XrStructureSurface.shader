Shader "SwanJourney/XR Structure Surface"
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
        _TrackContactMap("Fixed track contact",2D)="white" {}
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
            #pragma fragment StructureFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_local _ _STRUCTURE_AO
            #pragma multi_compile_local _ _TRACK_CONTACT _DETAIL_CONTACT
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile_local _ _METALLICSPECGLOSSMAP
            #pragma multi_compile_local _ _SPECULARHIGHLIGHTS_OFF
            #pragma multi_compile_local _ _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #define _OCCLUSIONMAP 1
            #define _EMISSION 1
            #define SWAN_STRUCTURE_ONLY 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "XrCanyonLighting.hlsl"
            half4 StructureFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color;LitPassFragment(input,color);return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
