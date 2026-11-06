#ifndef SWAN_CANYON_LIGHTING_INCLUDED
#define SWAN_CANYON_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE2D(_CanyonBakedMap); SAMPLER(sampler_CanyonBakedMap);
TEXTURE2D(_SwanCanyonLightA); SAMPLER(sampler_SwanCanyonLightA);
TEXTURE2D(_SwanCanyonLightB); SAMPLER(sampler_SwanCanyonLightB);
float _SwanCanyonLightEnabled,_SwanCanyonLightBlend;
half3 _SwanCanyonAmbientSky,_SwanCanyonAmbientHorizon,_SwanCanyonAmbientGround;
float _SwanStructureAoBlend;
#ifdef _CANYON_NIGHT_SAND
half _SwanSkyNight;
#endif
#ifdef _CANYON_TUNNEL
TEXTURE3D(_SwanTunnelField); SAMPLER(sampler_SwanTunnelField);
float4x4 _SwanTunnelSpace;
float3 _SwanTunnelMin,_SwanTunnelInv;
float4 _SwanTunnelSettings;
half CanyonSkyVisibility(float3 world)
{
    if(_SwanTunnelSettings.x<.5)return 1;
    float3 uv=(mul(_SwanTunnelSpace,float4(world,1)).xyz-_SwanTunnelMin)*_SwanTunnelInv;
    if(any(uv<0)||any(uv>1))return 1;
    return smoothstep(.12h,.92h,SAMPLE_TEXTURE3D_LOD(_SwanTunnelField,sampler_SwanTunnelField,uv,0).r);
}
#endif
#if defined(_TRACK_CONTACT) || defined(_DETAIL_CONTACT)
TEXTURE2D(_TrackContactMap); SAMPLER(sampler_TrackContactMap);
#endif
struct Attributes
{
    float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT;
    float2 texcoord:TEXCOORD0; float2 bakedUV:TEXCOORD1;
    half4 color:COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;
    float2 uv:TEXCOORD1; float2 bakedUV:TEXCOORD2;
    half3 normalWS:TEXCOORD3; half4 tangentWS:TEXCOORD4;
    half4 lightAndFog:TEXCOORD5; half3 vertexSH:TEXCOORD6;
    half occlusion:TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
Varyings LitPassVertex(Attributes input)
{
    Varyings output=(Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
    output.positionCS=p.positionCS;output.positionWS=p.positionWS;
    output.normalWS=n.normalWS;output.tangentWS=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());
    output.uv=TRANSFORM_TEX(input.texcoord,_BaseMap);output.bakedUV=input.bakedUV;
    output.lightAndFog=half4(VertexLighting(p.positionWS,n.normalWS),ComputeFogFactor(p.positionCS.z));
    output.vertexSH=SampleSHVertex(n.normalWS);
    #ifdef _STRUCTURE_AO
    output.occlusion=input.color.a;
    #else
    output.occlusion=1;
    #endif
    return output;
}
void LitPassFragment(Varyings input,out half4 color)
{
    UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surface;InitializeStandardLitSurfaceData(input.uv,surface);
    InputData data=(InputData)0;data.positionWS=input.positionWS;
    #ifdef _NORMALMAP
    half3 bitangent=input.tangentWS.w*cross(input.normalWS,input.tangentWS.xyz);
    data.normalWS=TransformTangentToWorld(surface.normalTS,half3x3(input.tangentWS.xyz,bitangent,input.normalWS));
    #else
    data.normalWS=input.normalWS;
    #endif
    data.normalWS=NormalizeNormalPerPixel(data.normalWS);
    data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
    data.vertexLighting=input.lightAndFog.xyz;
    data.fogCoord=InitializeInputDataFog(float4(input.positionWS,1),input.lightAndFog.w);
    data.bakedGI=SampleSHPixel(input.vertexSH,data.normalWS);
    data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
    data.shadowMask=half4(1,1,1,1);
    data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
    // Keep exposed faces unchanged; restore local cavity contrast without another light.
    half structure=lerp(1,input.occlusion*sqrt(saturate(input.occlusion)),_SwanStructureAoBlend);
    surface.occlusion=min(surface.occlusion,structure);
    surface.albedo*=lerp(.82h,1.0h,structure);
    #if defined(_TRACK_CONTACT) || defined(_DETAIL_CONTACT)
    half contact=SAMPLE_TEXTURE2D(_TrackContactMap,sampler_TrackContactMap,input.bakedUV).r;
    contact=lerp(1,contact*sqrt(saturate(contact)),_SwanStructureAoBlend);
    surface.occlusion=min(surface.occlusion,contact);
    // Offline contact is tight at joins and fades out, rather than tinting all surfaces.
    surface.albedo*=lerp(.38h,1.0h,contact);
    #endif
    #ifndef SWAN_STRUCTURE_ONLY
    half authoredOcclusion=surface.occlusion;
    half visibility=SAMPLE_TEXTURE2D(_CanyonBakedMap,sampler_CanyonBakedMap,input.bakedUV).r;
    // Short-range offline contact remains visible in bright daylight, right at
    // the support edge; it must not turn into a detached projected blob.
    surface.albedo*=lerp(.62,1,visibility);
    surface.occlusion=min(surface.occlusion,lerp(.3,1,visibility));
    if(_SwanCanyonLightEnabled>.5)
    {
        half3 irradiance=4*lerp(SAMPLE_TEXTURE2D(_SwanCanyonLightA,sampler_SwanCanyonLightA,input.bakedUV).rgb,
            SAMPLE_TEXTURE2D(_SwanCanyonLightB,sampler_SwanCanyonLightB,input.bakedUV).rgb,_SwanCanyonLightBlend);
        // Retain sky fill in deep recesses. The bake's direct-light occlusion must not
        // erase the live ambient term or amplify compressed near-black colour noise.
        // Explicit sky fill also works on receivers with light probes disabled.
        half3 skyFill=lerp(_SwanCanyonAmbientHorizon,
            data.normalWS.y>=0?_SwanCanyonAmbientSky:_SwanCanyonAmbientGround,abs(data.normalWS.y));
        // A readable sky/bounce floor prevents the non-directional bake from turning
        // the unlit sides of a tabletop rock into solid brown/black silhouettes.
        skyFill=max(skyFill,_SwanCanyonAmbientSky*.70);
        half skyLuminance=dot(_SwanCanyonAmbientSky,half3(.2126,.7152,.0722));
        half3 bounceFloor=lerp(skyLuminance.xxx,skyFill,.25)*2;
        half skyVisibility=1;
        #ifdef _CANYON_TUNNEL
        skyVisibility=CanyonSkyVisibility(data.positionWS);
        #endif
        // Outdoor fill must not illuminate the enclosed bore. Keep the local
        // light added below independent so the headlights can reveal its walls.
        irradiance=max(irradiance*lerp(.12h,1.h,skyVisibility),
            max(data.bakedGI*.85,bounceFloor)*lerp(.035h,1.h,skyVisibility*skyVisibility));
        #ifdef _CANYON_NIGHT_SAND
        // Grade only the sand's sky/bounce illumination. Direct headlights below
        // keep their warm colour and power, and daytime albedo stays unchanged.
        irradiance*=lerp(half3(1,1,1),half3(.52,.55,.58),saturate(_SwanSkyNight));
        #endif
        // Direct occlusion is already in irradiance; the ambient mask is a contact cue.
        surface.occlusion=min(authoredOcclusion,lerp(.78,1,visibility));
        Light main=GetMainLight(data.shadowCoord);
        // The static diffuse light is already baked. Only the train's live occlusion and
        // a small normal-map response are applied here, avoiding double direct lighting.
        half detail=clamp((.4+saturate(dot(data.normalWS,main.direction)))/
            (.4+saturate(dot(normalize(input.normalWS),main.direction))),.78,1.22);
        BRDFData brdf;InitializeBRDFData(surface,brdf);
        half3 lit=GlobalIllumination(brdf,irradiance*detail*lerp(.55,1,main.shadowAttenuation),surface.occlusion,
            data.positionWS,data.normalWS,data.viewDirectionWS);
        #ifdef _ADDITIONAL_LIGHTS
        uint count=GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(count)
            int index=GetPerObjectLightIndex(lightIndex);
            #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
            float punctual=_AdditionalLightsBuffer[index].position.w;
            #else
            float punctual=_AdditionalLightsPosition[index].w;
            #endif
            // The sun, moon and directional fill are already in the bake.
            if(punctual>.5)
            {
                Light lamp=GetAdditionalLight(lightIndex,data.positionWS);
                lit+=LightingPhysicallyBased(brdf,lamp,data.normalWS,data.viewDirectionWS);
            }
        LIGHT_LOOP_END
        #endif
        brdf.diffuse=0;
        lit+=LightingPhysicallyBased(brdf,main,data.normalWS,data.viewDirectionWS)+surface.emission;
        color=half4(MixFog(lit,data.fogCoord),1);return;
    }
    #endif
    color=UniversalFragmentPBR(data,surface);color.rgb=MixFog(color.rgb,data.fogCoord);color.a=1;
}
#endif
