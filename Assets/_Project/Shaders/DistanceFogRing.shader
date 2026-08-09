// A cylinder of fog, viewed from the inside.
//
// Exponential-squared fog alone cannot hide the edge of the world in
// Memory_CabinMorning: the terrain runs to +/-40 m and at the density the
// morning look wants (0.020) that edge is still better than half visible, while
// any density high enough to bury it also hazes the interior and dissolves the
// pines that are SUPPOSED to read as the boundary
// (Editor.MemoryExteriorBounds). So instead the edge gets occluded by geometry
// that IS the fog colour.
//
// Colour is read from unity_FogColor rather than a material property, so this
// can never drift out of sync with the RenderSettings that
// Editor.MemoryAtmosphereBuilder authors. Alpha falls off with world height:
// solid at ground level where the terrain edge is, gone by the top so there is
// no hard line against the sky.
//
// Cull Front because the camera is inside the cylinder. ZWrite Off so it never
// occludes anything in the depth buffer — it is a backdrop, not a wall (the
// actual out-of-bounds blocking is MemoryExteriorBounds' collider ring at
// +/-26 / +/-22, well inside this).
Shader "FalsePositive/DistanceFogRing"
{
    Properties
    {
        _FadeStartY("Fade Start (world Y)", Float) = 4
        _FadeEndY("Fade End (world Y)", Float) = 15
        _Opacity("Max Opacity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "FogRing"
            Tags { "LightMode"="UniversalForward" }

            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _FadeStartY;
                float _FadeEndY;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half fade = 1.0h - smoothstep(_FadeStartY, _FadeEndY, input.positionWS.y);
                return half4(unity_FogColor.rgb, _Opacity * fade);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
