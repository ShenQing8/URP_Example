Shader "URPMobileGrassInstancedIndirectDemo/GrassBendingRTPrePassPS"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "GrassBending" }
            Cull Off ZWrite Off ZTest Always Blend Zero SrcColor
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            CBUFFER_END
            float4x4 _GrassBendingVP;
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = mul(_GrassBendingVP, float4(TransformObjectToWorld(input.positionOS), 1));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex); output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color; }
            ENDHLSL
        }
    }
}
