// URP 14 metallic GGX, physical clear coat and derivative-based specular filtering.
// No emissive rim or second transparent shell: cast shadows and silhouettes stay geometric.
#include "StudioArrivalClip.hlsl"
void StudioMechanicalFragment(Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    StudioArrivalClip(StudioArrivalPosition(input.positionWS).y);
    SurfaceData surface;
    InitializeStandardLitSurfaceData(input.uv, surface);
    // Self-occlusion baked with rays against this mesh, never neighbouring detachable parts.
    surface.occlusion *= saturate(input.bakedCavity);
    InputData lighting;
    InitializeInputData(input, surface.normalTS, lighting);
    float3 dx = ddx(lighting.normalWS);
    float3 dy = ddy(lighting.normalWS);
    float variance = min(0.045, 0.35 * (dot(dx, dx) + dot(dy, dy)));
    float roughness = 1.0 - surface.smoothness;
    surface.smoothness = 1.0 - sqrt(saturate(roughness * roughness + variance));
    float coatRoughness = 1.0 - surface.clearCoatSmoothness;
    surface.clearCoatSmoothness = 1.0 - sqrt(saturate(coatRoughness * coatRoughness + variance));
    half4 color = UniversalFragmentPBR(lighting, surface);
    color.rgb += StudioArrivalEdge(StudioArrivalPosition(input.positionWS).y);
    color.rgb = MixFog(color.rgb, lighting.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
    outColor = color;
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
#endif
}
