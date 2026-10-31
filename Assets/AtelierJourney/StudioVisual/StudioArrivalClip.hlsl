#ifndef STUDIO_ARRIVAL_CLIP_INCLUDED
#define STUDIO_ARRIVAL_CLIP_INCLUDED
float _ArrivalNative21;
float4 _ArrivalRange;
float4x4 _ArrivalWorldToLocal;
float3 StudioArrivalPosition(float3 world){return mul(_ArrivalWorldToLocal,float4(world,1)).xyz;}
float StudioArrivalDistance(float y) {
    return lerp(-.04,1.04,_ArrivalRange.z)-(y-_ArrivalRange.x)/max(.001,_ArrivalRange.y);
}
void StudioArrivalClip(float y) {
    if (_ArrivalNative21>.5) clip(StudioArrivalDistance(y));
}
half3 StudioArrivalEdge(float y) {
    return _ArrivalNative21>.5 ? half3(.02,.52,.85)*(1-smoothstep(0,.013,StudioArrivalDistance(y))) : 0;
}
// Clip the integration interval itself, not the back face of a water volume.
void StudioArrivalRay(float3 ray, inout float nearT, inout float farT) {
    if(_ArrivalNative21>.5) {
        float3 origin=StudioArrivalPosition(_WorldSpaceCameraPos);
        ray=mul((float3x3)_ArrivalWorldToLocal,ray);
        float height=_ArrivalRange.x+_ArrivalRange.y*lerp(-.04,1.04,_ArrivalRange.z);
        if(abs(ray.y)<.00001) { if(origin.y>height) farT=nearT; }
        else { float hit=(height-origin.y)/ray.y;
            if(ray.y>0) farT=min(farT,hit); else nearT=max(nearT,hit); }
    }
}
#endif
