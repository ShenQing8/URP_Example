#ifndef MIGRATED_GRASS_COMMON_INCLUDED
#define MIGRATED_GRASS_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
CBUFFER_START(UnityPerMaterial)
float3 _PivotPosWS;
float2 _BoundSize;
float _GrassWidth, _GrassHeight;
float _WindAIntensity, _WindAFrequency;
float2 _WindATiling, _WindAWrap;
float _WindBIntensity, _WindBFrequency;
float2 _WindBTiling, _WindBWrap;
float _WindCIntensity, _WindCFrequency;
float2 _WindCTiling, _WindCWrap;
half4 _BaseColor;
float4 _BaseColorTexture_ST;
half4 _GroundColor;
half _RandomNormal;
CBUFFER_END
StructuredBuffer<float3> _AllInstancesTransformBuffer;
StructuredBuffer<uint> _VisibleInstanceOnlyTransformIDBuffer;
TEXTURE2D(_BaseColorTexture); SAMPLER(sampler_BaseColorTexture);
TEXTURE2D(_GrassBendingRT); SAMPLER(sampler_GrassBendingRT);
float _GrassBendingEnabled;
float4 _GrassBendingArea; // center XZ, full size XZ, supplied from actual world positions
float4x4 _GrassDepthVP;
struct GrassAttributes { float3 positionOS : POSITION; uint instanceID : SV_InstanceID; };
struct GrassVaryings {
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half3 albedo : TEXCOORD2;
    float2 heightFog : TEXCOORD3;
    half3 viewDirection : TEXCOORD4;
};
GrassVaryings GrassVertex(GrassAttributes input)
{
    InitIndirectDrawArgs(0);
    uint id = _VisibleInstanceOnlyTransformIDBuffer[GetIndirectInstanceID(input.instanceID)];
    float3 pivot = _AllInstancesTransformBuffer[id];
    float height = lerp(2, 5, sin(pivot.x * 23.4643 + pivot.z) * .45 + .55) * _GrassHeight;
    float2 uv = (pivot.xz - _GrassBendingArea.xy) / _GrassBendingArea.zw + .5;
    float stepped = 1;
    if (_GrassBendingEnabled > .5 && all(uv >= 0) && all(uv <= 1))
        stepped = SAMPLE_TEXTURE2D_LOD(_GrassBendingRT, sampler_GrassBendingRT, uv, 0).r;
    float3 right = UNITY_MATRIX_V[0].xyz;
    float3 up = UNITY_MATRIX_V[1].xyz;
    float3 forward = -UNITY_MATRIX_V[2].xyz;
    float3 local = input.positionOS.x * right * _GrassWidth * (sin(pivot.x * 95.4643 + pivot.z) * .45 + .55);
    local += input.positionOS.y * up;
    float3 bend = forward; bend.xz *= .5; bend.y = min(-.5, bend.y);
    local = lerp(local + bend * local.y / -bend.y, local, stepped * .95 + .05);
    local.y *= height;
    float3 view = _WorldSpaceCameraPos - pivot;
    local += right * input.positionOS.x * max(0, length(view) * .0225);
    float wind = (sin(_Time.y * _WindAFrequency + dot(pivot.xz, _WindATiling)) * _WindAWrap.x + _WindAWrap.y) * _WindAIntensity;
    wind += (sin(_Time.y * _WindBFrequency + dot(pivot.xz, _WindBTiling)) * _WindBWrap.x + _WindBWrap.y) * _WindBIntensity;
    wind += (sin(_Time.y * _WindCFrequency + dot(pivot.xz, _WindCTiling)) * _WindCWrap.x + _WindCWrap.y) * _WindCIntensity;
    wind *= input.positionOS.y;
    GrassVaryings output;
    output.positionWS = local + pivot + right * wind;
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = normalize(half3(0, 1, 0) + (_RandomNormal * sin(pivot.x * 82.32523 + pivot.z) - wind * .25) * right - forward * .5);
    half3 baseColor = SAMPLE_TEXTURE2D_LOD(_BaseColorTexture, sampler_BaseColorTexture, TRANSFORM_TEX(output.positionWS.xz, _BaseColorTexture), 0).rgb * _BaseColor.rgb;
    output.albedo = lerp(_GroundColor.rgb, baseColor, input.positionOS.y);
    output.heightFog = float2(local.y, ComputeFogFactor(output.positionCS.z));
    output.viewDirection = SafeNormalize(view); // Preserve the source's per-blade pivot view vector.
    return output;
}
GrassVaryings GrassDepthVertex(GrassAttributes input)
{
    GrassVaryings output = GrassVertex(input);
    output.positionCS = mul(_GrassDepthVP, float4(output.positionWS, 1));
    return output;
}
half3 ApplySingleDirectLight(Light light, half3 N, half3 V, half3 albedo, half height)
{
    half3 H = SafeNormalize(light.direction + V);
    half diffuse = dot(N, light.direction) * .5 + .5;
    float specular = saturate(dot(N, H));
    specular *= specular; specular *= specular; specular *= specular;
    specular *= .1 * height;
    return (albedo * diffuse + specular) * light.color * (light.shadowAttenuation * light.distanceAttenuation);
}
half4 GrassFragment(GrassVaryings input) : SV_Target
{
    half3 N = NormalizeNormalPerPixel(input.normalWS);
    half3 V = SafeNormalize(input.viewDirection);
    float4 shadowCoord;
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    half4 shadowMask = half4(1, 1, 1, 1);
    Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
    half3 color = SampleSH(half3(0, 0, 0)) * input.albedo;
    color += ApplySingleDirectLight(mainLight, N, V, input.albedo, input.heightFog.x);
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        color += ApplySingleDirectLight(light, N, V, input.albedo, input.heightFog.x);
    }
#endif
#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        color += ApplySingleDirectLight(light, N, V, input.albedo, input.heightFog.x);
    LIGHT_LOOP_END
#endif
#if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    color *= ao.indirectAmbientOcclusion;
#endif
    return half4(MixFog(color, input.heightFog.y), 1);
}
half4 GrassNormalsFragment(GrassVaryings input) : SV_Target
{
    float3 normal = NormalizeNormalPerPixel(input.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normal) * .5 + .5;
    return half4(PackFloat2To888(saturate(oct)), 0);
#else
    return half4(normal, 0);
#endif
}
#endif
