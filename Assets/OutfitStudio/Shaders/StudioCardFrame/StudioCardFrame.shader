Shader "Custom/StudioCardFrame"
{
    // Editor-only Outfit Studio "item card" frame, drawn as three camera-parented quads:
    //   _Mode 0 = Background  (fullscreen vertical gradient + optional radial glow, opaque, ZWrite On)
    //   _Mode 1 = Card panel  (rounded-rect, vertical gradient fill + optional border, alpha)
    //   _Mode 2 = Bottom fade (card rounded-rect mask * vertical fade to transparent)
    // Render state (ZTest/ZWrite/Blend) and the queue are driven from the material by
    // StudioCardFrame.cs so one shader covers all three layers. See IMPLEMENTATION.md §18.
    Properties
    {
        [Enum(Background,0,Card,1,Fade,2,SideMask,3,Border,4)] _Mode ("Mode", Float) = 0

        // Side-mask rect (mode 3), in the background quad's UV space: (left, right, bottom, top).
        _MaskRect ("Mask Rect (l,r,b,t)", Vector) = (0, 1, 0, 1)

        _ColorA ("Color A (top)", Color) = (0.09, 0.08, 0.23, 1)
        _ColorB ("Color B (bottom)", Color) = (0.23, 0.12, 0.36, 1)

        // Background radial glow
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 0)
        _HighlightCenter ("Highlight Center", Vector) = (0.5, 0.62, 0, 0)
        _HighlightSize ("Highlight Size", Vector) = (0.7, 0.7, 0, 0)

        // Decentraland loading-screen background (mode 0/3 only) — animated purple vignette with a
        // scrolling icon-pattern overlay, ported from Explorer's Custom/AnimatedBackgroundMovingTexture
        // (unity-explorer's TileableTexture.shader / BackgroundLoading.mat). See IMPLEMENTATION.md §18.
        [Toggle] _UseDclBg ("Use DCL Background", Float) = 0
        _DclOverlayTex ("DCL Overlay Tex", 2D) = "white" {}
        _DclInnerColor ("DCL Inner Color", Color) = (0.75, 0, 1, 1)
        _DclOuterColor ("DCL Outer Color", Color) = (0.3, 0, 0.5, 1)
        _DclRadius ("DCL Radius", Range(0,1)) = 0.42
        _DclSmoothness ("DCL Smoothness", Range(0.01,1)) = 0.55
        _DclOverlayColor ("DCL Overlay Color", Color) = (1, 1, 1, 1)
        _DclOverlayTiling ("DCL Overlay Tiling", Float) = 1.66
        _DclOverlayDirection ("DCL Overlay Direction", Vector) = (1, -1.25, 0, 0)
        _DclOverlaySpeed ("DCL Overlay Speed", Float) = 0.06
        _DclOverlayAlpha ("DCL Overlay Alpha", Range(0,1)) = 0.573
        _DclGlowColor ("DCL Glow Color", Color) = (0.66, 0, 0.745, 1)
        _DclGlowStrength ("DCL Glow Strength", Float) = 0.59
        _DclGlowCenter ("DCL Glow Center", Vector) = (0.68, 0.5, 0, 0)
        _DclGlowRadius ("DCL Glow Radius", Vector) = (0.05, -0.13, 0, 0)
        _DclGlowSmoothness ("DCL Glow Smoothness", Float) = 3.61
        _DclLuminosityStrength ("DCL Luminosity Strength", Range(0,1)) = 0.541

        // Card rounded-rect (also used by the fade so its bottom corners match)
        _CardAspect ("Card Aspect (w/h)", Float) = 0.66
        _CornerRadius ("Corner Radius", Range(0,1)) = 0.08
        _BorderColor ("Border Color", Color) = (0.72, 0.55, 0.88, 1)
        _BorderWidth ("Border Width", Range(0,0.2)) = 0.0
        _BorderTopFade ("Border Top Fade Start (uv.y)", Range(0,1)) = 0.88

        // Fade (mode 2)
        _FadeColor ("Fade Color", Color) = (0.23, 0.12, 0.36, 1)
        _FadeStart ("Fade Start (uv.y)", Range(0,1)) = 0.18
        _FadeEnd ("Fade End (uv.y)", Range(0,1)) = 0.4

        // Material-driven render state
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4   // LEqual
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1   // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0   // Zero
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StudioCardFrame"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            // Separate alpha blend: RGB uses the material-driven factors as before, but alpha always
            // uses the standard "over" formula (One, OneMinusSrcAlpha). With a single shared factor
            // pair, alpha blends as srcAlpha² + dstAlpha·(1-srcAlpha), which dips below 1 (as low as
            // 0.75) at any anti-aliased edge composited over an opaque layer beneath — invisible in
            // RGB (the painted color matches what's underneath) but a visible seam in the alpha
            // channel alone (e.g. compositing the exported PNG over a different background). The
            // correct "over" formula keeps alpha at 1 whenever the destination is already opaque,
            // which is always true here once the BG quad has drawn — so the whole card frame,
            // including the Fade quad's bottom gradient, now exports fully opaque, as intended.
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float _Mode;
            float4 _ColorA, _ColorB;
            float4 _HighlightColor;
            float2 _HighlightCenter, _HighlightSize;
            float _CardAspect, _CornerRadius, _BorderWidth, _BorderTopFade;
            float4 _BorderColor;
            float4 _FadeColor;
            float _FadeStart, _FadeEnd;
            float4 _MaskRect;

            float _UseDclBg;
            TEXTURE2D(_DclOverlayTex);
            SAMPLER(sampler_DclOverlayTex);
            float4 _DclInnerColor, _DclOuterColor;
            float _DclRadius, _DclSmoothness;
            float4 _DclOverlayColor;
            float _DclOverlayTiling;
            float2 _DclOverlayDirection;
            float _DclOverlaySpeed, _DclOverlayAlpha;
            float4 _DclGlowColor;
            float _DclGlowStrength;
            float2 _DclGlowCenter, _DclGlowRadius;
            float _DclGlowSmoothness, _DclLuminosityStrength;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // RGB <-> HSV, used by the DCL background's "luminosity blend" (recolors the overlay
            // pattern to the vignette's hue/saturation while keeping the pattern's own brightness).
            float3 RgbToHsv (float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb (float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // Ported from Explorer's Custom/AnimatedBackgroundMovingTexture (TileableTexture.shader):
            // a radial purple vignette with a scrolling, tinted icon-pattern overlay (luminosity blend)
            // and an off-center radial glow. Opaque (alpha handled by the caller), so no _Mode<3.5 mask.
            float3 DclBackground (float2 uv)
            {
                float radius = length(uv - 0.5);
                float mask = smoothstep(_DclRadius + _DclSmoothness, _DclRadius, radius);
                float3 vignette = lerp(_DclOuterColor.rgb, _DclInnerColor.rgb, mask);

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 overlayUv = uv * float2(_DclOverlayTiling * aspect, _DclOverlayTiling);
                overlayUv += _Time.y * _DclOverlayDirection * _DclOverlaySpeed;
                float4 overlay = SAMPLE_TEXTURE2D(_DclOverlayTex, sampler_DclOverlayTex, overlayUv) * _DclOverlayColor;
                overlay.a *= _DclOverlayAlpha * mask;

                float3 vignetteHsv = RgbToHsv(vignette);
                float3 overlayHsv = RgbToHsv(overlay.rgb);
                float v = lerp(0.5, 1.0, overlayHsv.z);
                float3 luminosityBlend = HsvToRgb(float3(vignetteHsv.x, vignetteHsv.y, v));
                float3 col = lerp(vignette, luminosityBlend, overlay.a * _DclLuminosityStrength);

                float2 glowDelta = (uv - _DclGlowCenter) / _DclGlowRadius;
                float glowMask = 1.0 - smoothstep(1.0, 1.0 + _DclGlowSmoothness, length(glowDelta));
                col += _DclGlowColor.rgb * glowMask * _DclGlowStrength * _DclGlowColor.a;
                return col;
            }

            // Signed distance to a rounded box; negative inside. Worked in a space where the card
            // half-height is 1 and half-width is the aspect, so the corner radius stays circular.
            float RoundedBoxSDF (float2 uv, float aspect, float radius)
            {
                float2 e = float2(aspect, 1.0);
                float r = min(radius, min(e.x, e.y));
                float2 p = (uv - 0.5) * 2.0 * e;
                float2 q = abs(p) - (e - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // --- Background (mode 0) / side-mask fill (mode 3) share the same gradient --------
                if (_Mode < 0.5 || (_Mode > 2.5 && _Mode < 3.5))
                {
                    float3 col;
                    if (_UseDclBg > 0.5)
                    {
                        col = DclBackground(uv);
                    }
                    else
                    {
                        col = lerp(_ColorB.rgb, _ColorA.rgb, uv.y);              // vertical gradient
                        float2 d = (uv - _HighlightCenter) / max(_HighlightSize, 1e-4);
                        float glow = 1.0 - smoothstep(0.0, 1.0, length(d));
                        col = lerp(col, _HighlightColor.rgb, glow * _HighlightColor.a);
                    }

                    if (_Mode < 0.5) return float4(col, 1.0);                    // opaque background

                    // Side mask: repaint the background OUTSIDE the card rect (clipping the avatar's
                    // arms/hands), but leave the top open above the card so the head still overflows.
                    float2 lo = _MaskRect.xz, hi = _MaskRect.yw;                 // (left,bottom),(right,top)
                    float2 cardUv = (uv - lo) / max(hi - lo, 1e-4);
                    float md = RoundedBoxSDF(cardUv, _CardAspect, _CornerRadius);
                    float maa = max(fwidth(md), 1e-5);
                    float cardMask = 1.0 - smoothstep(-maa, maa, md);           // inside the rounded card
                    float axu = max(fwidth(uv.x), 1e-5), ayu = max(fwidth(uv.y), 1e-5);
                    float withinX = smoothstep(lo.x - axu, lo.x + axu, uv.x)
                                  * (1.0 - smoothstep(hi.x - axu, hi.x + axu, uv.x));
                    float aboveTop = smoothstep(hi.y - ayu, hi.y + ayu, uv.y);   // open above the card top
                    // ADD (not max) the two keep-regions: at the card-top transition both the card
                    // mask and the overflow column are mid-fade (~0.5), and max(0.5,0.5)=0.5 dipped
                    // "inside" below 1, painting a faint bg line across the head. Their sum is ~1
                    // there (they're complementary in y), so the seam disappears; saturate caps it and
                    // they never both fully overlap elsewhere (aboveTop is 0 below the top).
                    float inside = saturate(cardMask + withinX * aboveTop);
                    return float4(col, 1.0 - inside);                            // paint bg only outside
                }

                // Shared rounded-rect mask for card + fade
                float dist = RoundedBoxSDF(uv, _CardAspect, _CornerRadius);
                float aa = max(fwidth(dist), 1e-5);
                float mask = 1.0 - smoothstep(-aa, aa, dist);                    // 1 inside, 0 outside

                // --- Card panel (mode 1) — fill only; the border is a separate top layer ---------
                if (_Mode < 1.5)
                {
                    float3 fill = lerp(_ColorB.rgb, _ColorA.rgb, uv.y);
                    return float4(fill, mask);
                }

                // --- Bottom fade (mode 2) -------------------------------------------------------
                if (_Mode < 2.5)
                {
                    float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, uv.y);   // opaque at bottom
                    return float4(_FadeColor.rgb, mask * fade);
                }

                // --- Border (mode 4) — drawn LAST, on top of the avatar / fade / side-mask ------
                // Ring in the band dist ∈ (-_BorderWidth, 0): inside the edge but not deep interior.
                // Written as the difference of two edge smoothsteps (outer at dist 0, inner at
                // dist -_BorderWidth) so the band collapses to EXACTLY zero when _BorderWidth is 0 —
                // the old mask*innerCut form peaked at ~0.25 on the edge, leaving a ~1px hairline
                // around the whole card even at width 0.
                // Faded out near the top so the border only frames the sides/bottom and the head
                // overflows the top freely (same intent as the side mask leaving the top open).
                float sOuter = smoothstep(-aa, aa, dist);                                // 0 inside → 1 outside
                float sInner = smoothstep(-_BorderWidth - aa, -_BorderWidth + aa, dist); // 0 deep-inside → 1 inward of ring
                float ring = saturate(sInner - sOuter);
                float topOpen = 1.0 - smoothstep(_BorderTopFade, 1.0, uv.y);
                return float4(_BorderColor.rgb, ring * topOpen * _BorderColor.a);
            }
            ENDHLSL
        }
    }
}
