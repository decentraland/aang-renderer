Shader "Custom/GradientBackground"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "GradientPass"
            ZTest LEqual
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            float4 _InnerColor;
            float4 _OuterColor;

            #pragma vertex Vert
            #pragma fragment frag

            float4 frag(Varyings IN) : SV_Target
            {
                float2 center = float2(0.5, 0.35);
                // float dist = distance(IN.texcoord, center);

                float3 inner_color = SRGBToLinear(_InnerColor.rgb);
                float3 outer_color = SRGBToLinear(_OuterColor.rgb);

                // float3 inner_color = _InnerColor.rgb; // center
                // float3 outer_color = _OuterColor.rgb; // edges

                // float t = saturate(dist * 2.0);
                float t = saturate(distance(IN.texcoord, center) / 0.7071);
                float3 color = lerp(inner_color, outer_color, t);

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}