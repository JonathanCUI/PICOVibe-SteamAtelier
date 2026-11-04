Shader "SwanJourney/XR Valley Surface"
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
            TEXTURE3D(_SwanTunnelField); SAMPLER(sampler_SwanTunnelField);
            float4x4 _SwanTunnelSpace;
            float3 _SwanTunnelMin,_SwanTunnelInv;
            float4 _SwanTunnelSettings;
            half4 SurfaceFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 solid; LitPassFragment(input,solid);
                if(_SwanTunnelSettings.x<.5)return solid;
                float3 uv=(mul(_SwanTunnelSpace,float4(input.positionWS,1)).xyz-_SwanTunnelMin)*_SwanTunnelInv;
                if(any(uv<0)||any(uv>1))return solid;
                half exposure=SAMPLE_TEXTURE3D_LOD(_SwanTunnelField,sampler_SwanTunnelField,uv,0).r;
                half shade=lerp(lerp(.38,.24,_SwanTunnelSettings.y),1,exposure);
                half3 emission=SampleEmission(input.uv,_EmissionColor.rgb,TEXTURE2D_ARGS(_EmissionMap,sampler_EmissionMap));
                // Leave authored emissive details luminous under the rock canopy.
                solid.rgb=max(0,solid.rgb-emission)*shade+emission;
                return solid;
            }
            ENDHLSL
        }
    }
}

