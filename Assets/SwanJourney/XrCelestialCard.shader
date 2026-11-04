Shader "SwanJourney/XR Celestial Card"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Optical profile", 2D) = "white" {}
        _Tint("Horizon tint and visibility", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ SOLAR_RAYS
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0;
                #if defined(SOLAR_RAYS)
                float2 rayOffset:TEXCOORD1;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                #if defined(SOLAR_RAYS)
                float2 p=input.uv-.5;
                float tilt=.055*sin(_Time.y*1.13);
                output.rayOffset=float2(-p.y*tilt+p.x*.12*sin(_Time.y*.83),p.x*tilt);
                #endif
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.uv;
                #if defined(SOLAR_RAYS)
                float2 p=uv-.5;
                uv+=input.rayOffset*smoothstep(.014,.055,dot(p,p));
                #endif
                half4 optics = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half alpha = optics.a * _Tint.a;
                half3 color = optics.rgb * lerp(_Tint.rgb, half3(1,1,1), optics.a * optics.a);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
