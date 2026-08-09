// URP port of the "Drunk Color Pulse" PPv2 asset (SynapticResponse). Same math as the
// original Assets/DrunkColorPulse/DrunkColorPulse.cs+.shader (deleted -- PPv2 does not
// run under URP): a previous-frame feedback smear, then a pulsing lerp toward an
// overlay colour. Only the plumbing changed -- PPv2's StdLib.hlsl/VertDefault are
// replaced with URP core's Blit.hlsl/Vert, and _MainTex is now _BlitTexture (what
// Blitter.BlitTexture binds the source to). Driven by
// Scripts/Rendering/DrunkColorPulseFeature.cs, never by Shader.Find -- that is why this
// no longer needs to live under Assets/Resources/.
Shader "Hidden/FalsePositive/DrunkColorPulse"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "DrunkColorPulseComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_PrevFrameTex);
            SAMPLER(sampler_PrevFrameTex);

            float4 _OverlayColor;
            int    _PulseEnabled;
            float  _PulseSpeed;
            float  _OverLayMaxIntensity;
            float  _TrailBlurStrength;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);

                if (_TrailBlurStrength > 0.0)
                {
                    float4 prevColor = SAMPLE_TEXTURE2D(_PrevFrameTex, sampler_PrevFrameTex, uv);
                    color = prevColor * _TrailBlurStrength + color * (1.0 - _TrailBlurStrength);
                }

                if (_OverLayMaxIntensity > 0.0)
                {
                    float level = _OverLayMaxIntensity;
                    if (_PulseEnabled) level = (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5) * _OverLayMaxIntensity;
                    color.rgb = lerp(color.rgb, _OverlayColor.rgb, level);
                }

                return color;
            }
            ENDHLSL
        }
    }
}
