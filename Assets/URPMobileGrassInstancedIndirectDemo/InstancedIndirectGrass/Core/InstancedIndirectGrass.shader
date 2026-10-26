Shader "MobileDrawMeshInstancedIndirect/SingleGrass"
{
    Properties
    {
        [MainColor] _BaseColor("BaseColor", Color) = (1,1,1,1)
        _BaseColorTexture("_BaseColorTexture", 2D) = "white" {}
        _GroundColor("_GroundColor", Color) = (0.5,0.5,0.5)

        [Header(Grass Shape)]
        _GrassWidth("_GrassWidth", Float) = 1
        _GrassHeight("_GrassHeight", Float) = 1

        [Header(Wind)]
        _WindAIntensity("_WindAIntensity", Float) = 1.77
        _WindAFrequency("_WindAFrequency", Float) = 4
        _WindATiling("_WindATiling", Vector) = (0.1,0.1,0)
        _WindAWrap("_WindAWrap", Vector) = (0.5,0.5,0)

        _WindBIntensity("_WindBIntensity", Float) = 0.25
        _WindBFrequency("_WindBFrequency", Float) = 7.7
        _WindBTiling("_WindBTiling", Vector) = (.37,3,0)
        _WindBWrap("_WindBWrap", Vector) = (0.5,0.5,0)


        _WindCIntensity("_WindCIntensity", Float) = 0.125
        _WindCFrequency("_WindCFrequency", Float) = 11.7
        _WindCTiling("_WindCTiling", Vector) = (0.77,3,0)
        _WindCWrap("_WindCWrap", Vector) = (0.5,0.5,0)

        [Header(Lighting)]
        _RandomNormal("_RandomNormal", Float) = 0.15

        //make SRP batcher happy
        [HideInInspector]_PivotPosWS("_PivotPosWS", Vector) = (0,0,0,0)
        [HideInInspector]_BoundSize("_BoundSize", Vector) = (1,1,0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "GrassCommon.hlsl"
        ENDHLSL
        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            ENDHLSL
        }
        // Drawn explicitly by the graph: built-in prepasses cannot declare our bending texture dependency.
        Pass
        {
            Name "GrassDepthNormals"
            Tags { "LightMode" = "GrassDepthNormals" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassDepthVertex
            #pragma fragment GrassNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
        Pass
        {
            Name "GrassDepthOnly"
            Tags { "LightMode" = "GrassDepthOnly" }
            Cull Back ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassDepthVertex
            #pragma fragment GrassNormalsFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
