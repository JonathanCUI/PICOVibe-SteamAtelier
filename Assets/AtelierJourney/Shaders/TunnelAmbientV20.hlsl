#ifndef TUNNEL_AMBIENT_V20
#define TUNNEL_AMBIENT_V20
TEXTURE3D(_TunnelAmbient20);SAMPLER(sampler_TunnelAmbient20);
float4x4 _TunnelWorldToLocal20;float3 _TunnelMin20,_TunnelInv20;float _TunnelActive20;
float TunnelVisibility20(float3 wp){
 if(_TunnelActive20<.5)return 1;
 float3 uv=(mul(_TunnelWorldToLocal20,float4(wp,1)).xyz-_TunnelMin20)*_TunnelInv20;
 if(any(uv<0)||any(uv>1))return 1;
 return SAMPLE_TEXTURE3D_LOD(_TunnelAmbient20,sampler_TunnelAmbient20,uv,0).r;
}
// Preserve layered terrain/rock shading while the entry plane reveals it.
float4 _ArrivalRange;float _ArrivalNative21;float4x4 _ArrivalWorldToLocal;
void ArrivalNativeClip21(float3 wp){
 if(_ArrivalNative21>.5)clip(_ArrivalRange.x+_ArrivalRange.y*lerp(-.04,1.04,_ArrivalRange.z)-mul(_ArrivalWorldToLocal,float4(wp,1)).y);
}
#endif
