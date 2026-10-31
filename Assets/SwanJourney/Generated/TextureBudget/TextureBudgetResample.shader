Shader "Hidden/SwanJourney/TextureBudgetResample" {
Properties { _MainTex ("Texture", 2D) = "white" {} }
SubShader { Cull Off ZWrite Off ZTest Always Pass {
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;
float _IsNormal, _PackedNormal, _EncodeSRGB;
float4 frag(v2f_img input) : SV_Target {
    float4 value = tex2D(_MainTex, input.uv);
    if (_IsNormal > .5) {
        float3 direction = _PackedNormal > .5 ? UnpackNormal(value) : value.rgb * 2 - 1;
        direction = normalize(direction + float3(0, 0, 1e-8));
        return float4(direction * .5 + .5, 1);
    }
    if (_EncodeSRGB > .5) value.rgb = LinearToGammaSpace(value.rgb);
    return value;
}
ENDCG
} } }
