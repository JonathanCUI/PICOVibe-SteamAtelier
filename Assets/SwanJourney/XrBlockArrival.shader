Shader "SwanJourney/XR Block Arrival"
{
    Properties
    {
        _BaseMap("Albedo",2D)="white" {}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _BumpMap("Normals",2D)="bump" {}
        _BumpScale("Normal scale",Float)=1
        _MetallicGlossMap("Metal and gloss",2D)="white" {}
        _Metallic("Metal",Float)=0
        _Smoothness("Smoothness",Float)=0.5
        _OcclusionMap("Occlusion",2D)="white" {}
        _OcclusionStrength("Occlusion strength",Float)=1
        _EmissionMap("Emission",2D)="white" {}
        _EmissionColor("Emission color",Color)=(0,0,0,1)
        _Surface("Surface",Float)=0
        _Cull("Cull",Float)=2
        _Slice("Local start, end, tile width, progress",Vector)=(0,1,.03,0)
        _CellWidth("Tile width",Float)=.03
        _SliceSlope("Fascia slope",Float)=0
        _CanyonBakedMap("Baked ambient visibility",2D)="white" {}
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
            #pragma fragment SliceFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile_local _ _METALLICSPECGLOSSMAP
            #pragma multi_compile_local _ _CANYON_BAKED
            #pragma multi_compile_local _ _CANYON_TUNNEL
            #pragma multi_compile_local _ _CANYON_NIGHT_SAND
            #pragma multi_compile_local _ _STRUCTURE_AO
            #pragma multi_compile_local _ _TRACK_CONTACT _DETAIL_CONTACT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #define _OCCLUSIONMAP 1
            #define _EMISSION 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #ifndef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #endif
            #if defined(_CANYON_BAKED) || defined(_STRUCTURE_AO) || defined(_TRACK_CONTACT) || defined(_DETAIL_CONTACT)
            #ifndef _CANYON_BAKED
            #define SWAN_STRUCTURE_ONLY 1
            #endif
            #include "XrCanyonLighting.hlsl"
            #else
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #endif
            float4x4 _SwanScanToLocal;
            float4 _Slice;
            float _CellWidth,_SliceSlope;
            float HashCell(float2 p)
            {
                float3 h=frac(float3(p.xyx)*float3(.1031,.11369,.13787));
                h+=dot(h,h.yzx+19.19);
                return frac((h.x+h.y)*h.z);
            }
            half4 SliceFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 pos=mul(_SwanScanToLocal,float4(input.positionWS,1)).xyz;
                float3 p=pos/_CellWidth;
                float front=lerp(_Slice.x,_Slice.y,_Slice.w);
                float column=HashCell(floor(p.xz));
                float depth=(front-pos.y-_SliceSlope*pos.z)/_Slice.z+(column-.5)*1.6;
                clip(depth);
                half4 solid;
                LitPassFragment(input,solid);
                if(depth>=2.2)return solid;
                float3 n=abs(mul((float3x3)_SwanScanToLocal,input.normalWS));
                float2 uv=n.y>max(n.x,n.z)?p.xz:(n.x>n.z?p.zy:p.xy);
                float2 edge=min(frac(uv),1-frac(uv));
                float2 gridEdge=1-smoothstep(.035,.055+min(fwidth(uv),.3),edge);
                float grid=max(gridEdge.x,gridEdge.y);
                float tile=HashCell(floor(uv));
                // Gaps at the advancing edge expose the real room and farther facets.
                clip(depth-(.22+.78*(1-tile))*(1-grid));
                half3 blue=.32*solid.rgb+lerp(half3(.012,.055,.21),half3(.025,.27,.59),tile);
                float rim=1-smoothstep(.08,.48,abs(depth-1.35));
                blue=lerp(blue,half3(.085,.77,1),max(grid*.85,rim));
                return half4(lerp(blue,solid.rgb,smoothstep(1.45,2.2,depth)),1);
            }
            ENDHLSL
        }
    }
}
