Shader "Hidden/SwanJourney/BakeValleyCloud"
{
    SubShader { Pass {
        ZTest Always ZWrite Off Cull Off
        HLSLPROGRAM
        #pragma target 3.5
        #pragma vertex vert
        #pragma fragment frag
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE3D(_Noise); SAMPLER(sampler_Noise);
        float _Seed, _Phase, _Steam;
        float3 _LightOS;
        // Offline only: softer sky lobes do not alter the shared chimney density.
        #define SWAN_SOFT_SKY 1
        #include "Assets/AtelierJourney/Shaders/CloudDensityV16.hlsl"

        struct Varyings { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
        Varyings vert(float4 p:POSITION,float2 uv:TEXCOORD0)
        {
            Varyings o;o.pos=TransformObjectToHClip(p.xyz);o.uv=uv;return o;
        }
        half4 frag(Varyings i):SV_Target
        {
            // 64 slices in an 8 x 8 sheet, sampled at voxel centres.
            float2 tile=i.uv*8;
            float slice=floor(tile.x)+8*floor(tile.y);
            float3 p=float3(frac(tile),(slice+.5)/64)-.5;
            float d=density(p);
            float shade=density(p+_LightOS*.055)*.48+density(p+_LightOS*.12)*.63+density(p+_LightOS*.23)*.77;
            float lighting=saturate(exp(-shade*1.30)*.88+smoothstep(-.34,.30,p.y)*.12);
            return half4(d,lighting,0,1);
        }
        ENDHLSL
    } }
}
