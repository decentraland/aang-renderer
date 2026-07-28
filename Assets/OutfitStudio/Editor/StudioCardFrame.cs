using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Composites a Fortnite-style "item card" frame around the previewed avatar, entirely inside
    /// the studio scene and captured for free (it's camera geometry, not a UI overlay — runtime UI
    /// overlays don't render through the capture camera; see IMPLEMENTATION.md §8/§18).
    ///
    /// Three camera-parented quads, ordered purely by render queue so no per-avatar depth math is
    /// needed:
    ///   • Background (queue 1000, ZWrite On) — fullscreen gradient + glow; writes depth so the
    ///     skybox is occluded without touching the camera's clear flags.
    ///   • Card panel (queue 1500, ZTest Always) — rounded rect behind the avatar. The avatar
    ///     (opaque, queue 2000) draws over it, so the head overflowing the top edge is free.
    ///   • Bottom fade (queue 3500, ZTest Always) — drawn after the avatar; fades the legs into the
    ///     card colour, clipped to the same rounded rect so its bottom corners match.
    ///
    /// Poll-based and studio-scene-gated like StudioAvatarShaderSwitcher / the pipeline switcher.
    /// The quads are HideFlags.DontSave (never serialized into the scene) and recreated after a
    /// domain reload or play-mode scene reload; nothing ships to a build.
    /// </summary>
    [InitializeOnLoad]
    public static class StudioCardFrame
    {
        private const string ROOT_NAME = "__OutfitStudio_CardFrame";
        private const string SHADER_NAME = "Custom/StudioCardFrame";
        private const float PLANE_Z = 50f; // camera-local Z; safely behind a ~2 m avatar, well inside the far plane
        private const float BG_OVERSIZE = 1.04f; // background quad scale over the frustum (hides edge slivers)

        // The border quad's own scale must exceed the card's by enough that the shader's mode-4 UV
        // remap (see StudioCardFrame.shader) has physical room to paint the outer ring past the card
        // edge, even at the slider's max width. Recomputed per Layout() from the card's own aspect —
        // see the comment there for the derivation.
        private const float MAX_BORDER_WIDTH = 0.2f; // matches both border-width shader Range(0,0.2) sliders
        private const float BORDER_OVERSIZE_MARGIN = 0.05f; // extra slack past the slider max for AA softening

        // Ported from Explorer's loading-screen background; see the shader's DclBackground() comment.
        private const string DCL_BG_TEXTURE_PATH = "Assets/OutfitStudio/Textures/DclBackgroundPattern.png";

        // EditorPrefs keys
        private const string K_ENABLED = "OutfitStudio.Card.Enabled";
        private const string K_BG_ENABLED = "OutfitStudio.Card.BgEnabled";
        private const string K_USE_DCL_BG = "OutfitStudio.Card.UseDclBg";
        private const string K_SIDEMASK = "OutfitStudio.Card.SideMask";
        private const string K_BG_TOP = "OutfitStudio.Card.BgTop";
        private const string K_BG_BOTTOM = "OutfitStudio.Card.BgBottom";
        private const string K_GLOW = "OutfitStudio.Card.Glow";
        private const string K_GLOW_H = "OutfitStudio.Card.GlowHeight";
        private const string K_GLOW_S = "OutfitStudio.Card.GlowSize";
        private const string K_CARD_TOP = "OutfitStudio.Card.CardTop";
        private const string K_CARD_BOTTOM = "OutfitStudio.Card.CardBottom";
        private const string K_MARGIN_X = "OutfitStudio.Card.MarginX";
        private const string K_MARGIN_TOP = "OutfitStudio.Card.MarginTop";
        private const string K_MARGIN_BOTTOM = "OutfitStudio.Card.MarginBottom";
        private const string K_RADIUS = "OutfitStudio.Card.Radius";
        private const string K_BORDER = "OutfitStudio.Card.Border";
        private const string K_INNER_BORDER_W = "OutfitStudio.Card.BorderWidth"; // key unchanged so existing tuned values carry over from before the inner/outer split
        private const string K_OUTER_BORDER_W = "OutfitStudio.Card.OuterBorderWidth";
        private const string K_FADE = "OutfitStudio.Card.Fade";
        private const string K_FADE_H = "OutfitStudio.Card.FadeHeight";
        private const string K_FADE_S = "OutfitStudio.Card.FadeSoftness";

        // Shader property ids
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        private static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightCenterId = Shader.PropertyToID("_HighlightCenter");
        private static readonly int HighlightSizeId = Shader.PropertyToID("_HighlightSize");
        private static readonly int CardAspectId = Shader.PropertyToID("_CardAspect");
        private static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
        private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
        private static readonly int InnerBorderWidthId = Shader.PropertyToID("_InnerBorderWidth");
        private static readonly int OuterBorderWidthId = Shader.PropertyToID("_OuterBorderWidth");
        private static readonly int BorderOversizeId = Shader.PropertyToID("_BorderOversize");
        private static readonly int FadeColorId = Shader.PropertyToID("_FadeColor");
        private static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        private static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");
        private static readonly int MaskRectId = Shader.PropertyToID("_MaskRect");
        private static readonly int BorderTopFadeId = Shader.PropertyToID("_BorderTopFade");
        private static readonly int UseDclBgId = Shader.PropertyToID("_UseDclBg");
        private static readonly int DclOverlayTexId = Shader.PropertyToID("_DclOverlayTex");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        // Sensible defaults tuned to the reference cards (purple card on dark-indigo → violet bg).
        private static readonly Color DefBgTop = Hex("#16143A");
        private static readonly Color DefBgBottom = Hex("#3A1E5C");
        private static readonly Color DefGlow = new(0.42f, 0.30f, 0.58f, 0.25f);
        private static readonly Color DefCardTop = Hex("#6B3FA0");
        private static readonly Color DefCardBottom = Hex("#4A2870");
        private static readonly Color DefBorder = Hex("#B98CE0");
        private static readonly Color DefFade = Hex("#4A2870");

        private static GameObject _root;
        private static Renderer _bg, _card, _fade, _mask, _border;
        private static float _borderOversize = 1f; // set by Layout(), consumed by PushParams()
        private static double _nextCheck;
        private static bool _warnedMissingShader;

        static StudioCardFrame()
        {
            EditorApplication.update += Update;
        }

        // --- Persisted properties (setters push immediately, like the shader switcher) -----------

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(K_ENABLED, false);
            set { EditorPrefs.SetBool(K_ENABLED, value); Refresh(); }
        }

        /// <summary>Draw the fullscreen gradient background quad. On by default (identical to the
        /// original look). Turning it off leaves the card/fade/border/mask untouched, so with a
        /// transparent-clear capture the frame area outside the card is transparent while the card
        /// panel and the avatar stay opaque — the background quad is what forces the whole capture
        /// opaque (see the shader's "over" alpha blend comment), so skipping it is all this needs.</summary>
        public static bool BackgroundEnabled
        {
            get => EditorPrefs.GetBool(K_BG_ENABLED, true);
            set { EditorPrefs.SetBool(K_BG_ENABLED, value); Refresh(); }
        }

        /// <summary>Replace the gradient background (and the side-mask repaint, if that's on too)
        /// with the animated purple pattern from the Decentraland Explorer loading screens. Off by
        /// default. No effect while <see cref="BackgroundEnabled"/> is off.</summary>
        public static bool UseDclBackground
        {
            get => EditorPrefs.GetBool(K_USE_DCL_BG, false);
            set { EditorPrefs.SetBool(K_USE_DCL_BG, value); Refresh(); }
        }

        private static Texture2D _dclBgTexture;

        private static Texture2D DclBgTexture =>
            _dclBgTexture ??= AssetDatabase.LoadAssetAtPath<Texture2D>(DCL_BG_TEXTURE_PATH);

        /// <summary>Clip the avatar to the card's sides/bottom (arms/hands that spill past the card
        /// edge are hidden), leaving the top open so the head still overflows. Off by default.</summary>
        public static bool SideMask
        {
            get => EditorPrefs.GetBool(K_SIDEMASK, false);
            set { EditorPrefs.SetBool(K_SIDEMASK, value); Refresh(); }
        }

        /// <summary>Suppress the avatar's outline (a thin silhouette line, visible over the head
        /// against a light card) for clean beauty shots. Drives <see cref="Loading.AvatarLoader"/>'s
        /// runtime flag; independent of <see cref="Enabled"/> so it works with or without the frame.
        /// Deliberately NOT persisted to EditorPrefs (unlike the other card settings) — it always
        /// starts off on a fresh domain reload/Editor launch, so it can never silently stay on across
        /// a session and leave someone wondering why the outline is missing.</summary>
        public static bool HideOutline
        {
            get => _hideOutline;
            set { _hideOutline = value; SyncOutline(); }
        }

        private static bool _hideOutline;

        /// <summary>
        /// Live override for the studio camera's post-process antialiasing mode, so SMAA's edge
        /// erosion of the (thin) outline stroke can be compared against None/FXAA/TAA live. Null =
        /// leave the camera at whatever the scene/prefab has configured. Only settable in play mode
        /// (that's when the actual rendering camera exists); not persisted. (Outline width lives in
        /// StudioAvatarShaderSwitcher's knob list, the single owner of _Outline_Width.)
        /// </summary>
        public static AntialiasingMode? DebugAntialiasing
        {
            get => _debugAntialiasing;
            set { _debugAntialiasing = value; SyncDebugOverrides(); }
        }

        private static AntialiasingMode? _debugAntialiasing;
        private static AntialiasingMode? _originalAntialiasing; // captured on first override, restored when cleared

        /// <summary>Re-applies the antialiasing override. Called from the poll so it survives a
        /// play-mode re-entry (which would otherwise reset the camera to its authored default).</summary>
        private static void SyncDebugOverrides()
        {
            if (SceneManager.GetActiveScene().path != OutfitStudioWindow.STUDIO_SCENE_PATH) return;

            // The antialiasing mode lives on the actual rendering camera, which only exists once play
            // mode spins up the scene for real (edit-mode preview renders through the Scene View).
            if (!Application.isPlaying) return;

            var cam = FindCamera();
            var camData = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
            if (camData == null) return;

            if (_debugAntialiasing.HasValue)
            {
                _originalAntialiasing ??= camData.antialiasing;
                camData.antialiasing = _debugAntialiasing.Value;
            }
            else if (_originalAntialiasing.HasValue)
            {
                camData.antialiasing = _originalAntialiasing.Value;
                _originalAntialiasing = null;
            }
        }

        public static Color BgTop { get => GetColor(K_BG_TOP, DefBgTop); set => SetColor(K_BG_TOP, value); }
        public static Color BgBottom { get => GetColor(K_BG_BOTTOM, DefBgBottom); set => SetColor(K_BG_BOTTOM, value); }
        public static Color Glow { get => GetColor(K_GLOW, DefGlow, true); set => SetColor(K_GLOW, value); }
        public static float GlowHeight { get => EditorPrefs.GetFloat(K_GLOW_H, 0.62f); set => SetFloat(K_GLOW_H, value); }
        public static float GlowSize { get => EditorPrefs.GetFloat(K_GLOW_S, 0.7f); set => SetFloat(K_GLOW_S, value); }
        public static Color CardTop { get => GetColor(K_CARD_TOP, DefCardTop); set => SetColor(K_CARD_TOP, value); }
        public static Color CardBottom { get => GetColor(K_CARD_BOTTOM, DefCardBottom); set => SetColor(K_CARD_BOTTOM, value); }
        public static float MarginX { get => EditorPrefs.GetFloat(K_MARGIN_X, 0.06f); set => SetFloat(K_MARGIN_X, value); }
        public static float MarginTop { get => EditorPrefs.GetFloat(K_MARGIN_TOP, 0.12f); set => SetFloat(K_MARGIN_TOP, value); }
        public static float MarginBottom { get => EditorPrefs.GetFloat(K_MARGIN_BOTTOM, 0.05f); set => SetFloat(K_MARGIN_BOTTOM, value); }
        public static float CornerRadius { get => EditorPrefs.GetFloat(K_RADIUS, 0.08f); set => SetFloat(K_RADIUS, value); }
        public static Color Border { get => GetColor(K_BORDER, DefBorder); set => SetColor(K_BORDER, value); }
        public static float InnerBorderWidth { get => EditorPrefs.GetFloat(K_INNER_BORDER_W, 0f); set => SetFloat(K_INNER_BORDER_W, value); }
        public static float OuterBorderWidth { get => EditorPrefs.GetFloat(K_OUTER_BORDER_W, 0f); set => SetFloat(K_OUTER_BORDER_W, value); }
        public static Color Fade { get => GetColor(K_FADE, DefFade); set => SetColor(K_FADE, value); }
        public static float FadeHeight { get => EditorPrefs.GetFloat(K_FADE_H, 0.4f); set => SetFloat(K_FADE_H, value); }
        public static float FadeSoftness { get => EditorPrefs.GetFloat(K_FADE_S, 0.55f); set => SetFloat(K_FADE_S, value); }

        public static void ResetDefaults()
        {
            foreach (var k in new[]
                     {
                         K_BG_TOP, K_BG_BOTTOM, K_GLOW, K_GLOW_H, K_GLOW_S, K_CARD_TOP, K_CARD_BOTTOM,
                         K_MARGIN_X, K_MARGIN_TOP, K_MARGIN_BOTTOM, K_RADIUS, K_BORDER, K_INNER_BORDER_W,
                         K_OUTER_BORDER_W, K_FADE, K_FADE_H, K_FADE_S
                     })
                EditorPrefs.DeleteKey(k);
            Refresh();
        }

        // --- Poll + refresh ----------------------------------------------------------------------

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 0.5;
            SyncOutline();
            SyncDebugOverrides();
            Refresh();
        }

        /// <summary>
        /// Push the outline-suppression flag onto the runtime loaders. Only overrides inside the
        /// studio scene, so the outline behaves normally in the main app / other scenes. Re-applied
        /// every poll tick so it survives a domain reload or entering play mode (where the static
        /// resets). The flag is only read while playing (the outline renders in play mode).
        /// </summary>
        private static void SyncOutline()
        {
            // Suppress only while the studio window is open in the studio scene, so closing the
            // window or leaving the scene auto-restores the outline (the poll runs every tick, so a
            // stale "on" preference can never leave the outline stuck off with no visible control).
            var inStudio = SceneManager.GetActiveScene().path == OutfitStudioWindow.STUDIO_SCENE_PATH;
            var windowOpen = EditorWindow.HasOpenInstances<OutfitStudioWindow>();
            Loading.AvatarLoader.OutlineSuppressed = inStudio && windowOpen && HideOutline;
        }

        /// <summary>Ensure/teardown the quads and push the current settings. Cheap and idempotent.</summary>
        public static void Refresh()
        {
            var inStudio = SceneManager.GetActiveScene().path == OutfitStudioWindow.STUDIO_SCENE_PATH;
            if (!inStudio || !Enabled)
            {
                Teardown();
                return;
            }

            var cam = FindCamera();
            if (cam == null) { Teardown(); return; }

            if (_root == null && !TryReattach())
            {
                if (!Create(cam)) return; // shader missing — warned once inside Create
            }

            // Keep the frame parented to whatever camera renders now (it can change across play mode).
            if (_root.transform.parent != cam.transform)
                _root.transform.SetParent(cam.transform, false);

            Layout(cam);
            PushParams();
        }

        /// <summary>
        /// Re-lays-out for a specific camera/aspect — called by OutfitCapture right before a still so
        /// the card matches the capture resolution even if it differs from the Game view aspect.
        /// </summary>
        public static void RelayoutFor(Camera cam)
        {
            if (_root == null || cam == null) return;
            Layout(cam);
            PushParams(); // keep _CardAspect/_CornerRadius in sync with the capture's aspect, not the
                           // Game view's — otherwise the rounded-corner/border math is evaluated for
                           // the wrong aspect and a seam opens between quads only in the capture.
        }

        public static bool IsActive => _root != null;

        private static void Teardown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            _bg = _card = _fade = _mask = _border = null;
        }

        private static bool TryReattach()
        {
            // After a domain reload the static refs are gone but a DontSave root may survive; find it.
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name != ROOT_NAME) continue;
                if (go.scene != SceneManager.GetActiveScene()) continue;
                _root = go;
                _bg = FindChild("BG");
                _card = FindChild("Card");
                _fade = FindChild("Fade");
                _mask = FindChild("Mask");
                _border = FindChild("Border");
                if (_bg != null && _card != null && _fade != null && _mask != null && _border != null)
                    return true;
                Object.DestroyImmediate(go); // malformed — rebuild from scratch
                _root = null;
                return false;
            }
            return false;
        }

        private static Renderer FindChild(string n)
        {
            var t = _root.transform.Find(n);
            return t != null ? t.GetComponent<Renderer>() : null;
        }

        private static bool Create(Camera cam)
        {
            var shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                if (!_warnedMissingShader)
                {
                    Debug.LogWarning($"[OutfitStudio] Shader \"{SHADER_NAME}\" not found — card frame skipped. " +
                                     "Check the Console for compile errors or reimport Assets/OutfitStudio/Shaders.");
                    _warnedMissingShader = true;
                }
                return false;
            }
            _warnedMissingShader = false;

            _root = new GameObject(ROOT_NAME) { hideFlags = HideFlags.DontSave };
            _root.transform.SetParent(cam.transform, false);

            // queue, mode, and render state per layer
            _bg = MakeQuad("BG", shader, mode: 0, queue: 1000,
                zTest: (int)CompareFunction.LessEqual, zWrite: 1,
                src: (int)BlendMode.One, dst: (int)BlendMode.Zero);
            // LessEqual (not Always): the card panel sits behind the avatar (far Z), so it must
            // respect depth. The avatar outline draws BeforeRenderingOpaques and writes near depth in
            // its ring; with ZTest Always the card painted over that ring (outline showed the card
            // color). LessEqual lets the card draw over the BG quad (same far Z) but leaves the nearer
            // outline ring — and the opaque avatar — untouched.
            _card = MakeQuad("Card", shader, mode: 1, queue: 1500,
                zTest: (int)CompareFunction.LessEqual, zWrite: 0,
                src: (int)BlendMode.SrcAlpha, dst: (int)BlendMode.OneMinusSrcAlpha);
            _fade = MakeQuad("Fade", shader, mode: 2, queue: 3500,
                zTest: (int)CompareFunction.Always, zWrite: 0,
                src: (int)BlendMode.SrcAlpha, dst: (int)BlendMode.OneMinusSrcAlpha);
            // Side mask sits in front of the avatar (queue 3200, after opaque + transparent
            // wearables) but before the bottom fade; only enabled when SideMask is on.
            _mask = MakeQuad("Mask", shader, mode: 3, queue: 3200,
                zTest: (int)CompareFunction.Always, zWrite: 0,
                src: (int)BlendMode.SrcAlpha, dst: (int)BlendMode.OneMinusSrcAlpha);
            // Border is drawn LAST (queue 4000) so the card outline sits on top of the avatar,
            // the bottom fade, and the side mask.
            _border = MakeQuad("Border", shader, mode: 4, queue: 4000,
                zTest: (int)CompareFunction.Always, zWrite: 0,
                src: (int)BlendMode.SrcAlpha, dst: (int)BlendMode.OneMinusSrcAlpha);
            return true;
        }

        private static Renderer MakeQuad(string name, Shader shader, float mode, int queue,
            int zTest, int zWrite, int src, int dst)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(_root.transform, false);

            var mat = new Material(shader) { hideFlags = HideFlags.DontSave, renderQueue = queue };
            mat.SetFloat(ModeId, mode);
            mat.SetFloat(ZTestId, zTest);
            mat.SetFloat(ZWriteId, zWrite);
            mat.SetFloat(SrcBlendId, src);
            mat.SetFloat(DstBlendId, dst);

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        // --- Geometry & params -------------------------------------------------------------------

        private static void Layout(Camera cam)
        {
            // Frustum extents at PLANE_Z (vertical FOV; aspect from the camera = capture/Game view).
            var h = 2f * PLANE_Z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var w = h * cam.aspect;

            // Background: full frame (slightly oversized to hide any edge sliver on aspect mismatch).
            _bg.transform.localScale = new Vector3(w * BG_OVERSIZE, h * BG_OVERSIZE, 1f);
            _bg.transform.localPosition = new Vector3(0f, 0f, PLANE_Z);
            _bg.transform.localRotation = Quaternion.identity;

            // Side mask shares the background's exact transform so its repainted gradient is
            // pixel-identical (no seam); the card rect is passed to it as _MaskRect in this UV space.
            _mask.transform.localScale = _bg.transform.localScale;
            _mask.transform.localPosition = _bg.transform.localPosition;
            _mask.transform.localRotation = Quaternion.identity;

            // Card rect in viewport fractions: x ∈ [mL, 1-mR], y ∈ [mB, 1-mT] (y up).
            float mL = MarginX, mR = MarginX, mT = MarginTop, mB = MarginBottom;
            var cw = w * Mathf.Max(0.01f, 1f - mL - mR);
            var ch = h * Mathf.Max(0.01f, 1f - mT - mB);
            var cx = ((mL + (1f - mR)) * 0.5f - 0.5f) * w; // world offset of the card centre
            var cy = ((mB + (1f - mT)) * 0.5f - 0.5f) * h;

            foreach (var r in new[] { _card, _fade })
            {
                r.transform.localScale = new Vector3(cw, ch, 1f);
                r.transform.localPosition = new Vector3(cx, cy, PLANE_Z);
                r.transform.localRotation = Quaternion.identity;
            }

            // The border quad is scaled up beyond the card so the shader (mode 4) has physical room
            // to paint the outer ring past the card edge; it remaps its raw UV back into the card's
            // normalized SDF space by this same factor (see _BorderOversize in the shader). Derived
            // from the card's own aspect: in RoundedBoxSDF's normalized space the box half-extents
            // are (aspect, 1), so the tightest reach direction — straight out from a flat edge — is
            // whichever of those two is smaller; the oversize must clear the slider's max width in
            // that direction, plus a little slack for the AA smoothstep band.
            var cardAspect = cw / Mathf.Max(ch, 1e-4f);
            var minExtent = Mathf.Max(Mathf.Min(cardAspect, 1f), 0.01f);
            _borderOversize = 1f + (MAX_BORDER_WIDTH + BORDER_OVERSIZE_MARGIN) / minExtent;
            _border.transform.localScale = new Vector3(cw * _borderOversize, ch * _borderOversize, 1f);
            _border.transform.localPosition = new Vector3(cx, cy, PLANE_Z);
            _border.transform.localRotation = Quaternion.identity;
        }

        private static void PushParams()
        {
            var cardAspect = AspectOf(_card); // cw / ch

            _bg.enabled = BackgroundEnabled;
            var bg = _bg.sharedMaterial;
            bg.SetColor(ColorAId, BgTop);
            bg.SetColor(ColorBId, BgBottom);
            bg.SetColor(HighlightColorId, Glow);
            bg.SetVector(HighlightCenterId, new Vector4(0.5f, GlowHeight, 0f, 0f));
            bg.SetVector(HighlightSizeId, new Vector4(GlowSize, GlowSize, 0f, 0f));
            bg.SetFloat(UseDclBgId, UseDclBackground ? 1f : 0f);
            if (UseDclBackground && DclBgTexture != null) bg.SetTexture(DclOverlayTexId, DclBgTexture);

            var card = _card.sharedMaterial;
            card.SetColor(ColorAId, CardTop);
            card.SetColor(ColorBId, CardBottom);
            card.SetFloat(CardAspectId, cardAspect);
            card.SetFloat(CornerRadiusId, CornerRadius);

            // Border is its own top-most quad (drawn over the avatar/fade/mask), not baked into the card.
            var border = _border.sharedMaterial;
            border.SetFloat(CardAspectId, cardAspect);
            border.SetFloat(CornerRadiusId, CornerRadius);
            border.SetColor(BorderColorId, Border);
            border.SetFloat(InnerBorderWidthId, InnerBorderWidth);
            border.SetFloat(OuterBorderWidthId, OuterBorderWidth);
            border.SetFloat(BorderOversizeId, _borderOversize);
            border.SetFloat(BorderTopFadeId, 0.88f); // fade the border out over the top 12% (head overflow)

            var fade = _fade.sharedMaterial;
            fade.SetColor(FadeColorId, Fade);
            fade.SetFloat(CardAspectId, cardAspect);
            fade.SetFloat(CornerRadiusId, CornerRadius);
            var end = Mathf.Clamp01(FadeHeight);
            fade.SetFloat(FadeEndId, end);
            fade.SetFloat(FadeStartId, end * (1f - Mathf.Clamp01(FadeSoftness)));

            // Side mask: same gradient as the background, plus the card rect in the (oversized) bg UV
            // space. U() maps a viewport fraction to that space. Enabled only when the toggle is on.
            _mask.enabled = SideMask;
            if (SideMask)
            {
                var mask = _mask.sharedMaterial;
                mask.SetColor(ColorAId, BgTop);
                mask.SetColor(ColorBId, BgBottom);
                mask.SetColor(HighlightColorId, Glow);
                mask.SetVector(HighlightCenterId, new Vector4(0.5f, GlowHeight, 0f, 0f));
                mask.SetVector(HighlightSizeId, new Vector4(GlowSize, GlowSize, 0f, 0f));
                mask.SetFloat(UseDclBgId, UseDclBackground ? 1f : 0f);
                if (UseDclBackground && DclBgTexture != null) mask.SetTexture(DclOverlayTexId, DclBgTexture);
                mask.SetFloat(CardAspectId, cardAspect);
                mask.SetFloat(CornerRadiusId, CornerRadius);
                float U(float f) => 0.5f + (f - 0.5f) / BG_OVERSIZE;
                mask.SetVector(MaskRectId, new Vector4(
                    U(MarginX), U(1f - MarginX), U(MarginBottom), U(1f - MarginTop)));
            }

            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private static float AspectOf(Renderer r)
        {
            var s = r.transform.localScale;
            return s.y > 1e-4f ? s.x / s.y : 0.66f;
        }

        // internal (not private): reused by StudioFlyCameraController, which needs the exact same
        // "which camera is actually live" resolution — the studio scene can have more than one
        // GameObject tagged MainCamera (e.g. before the Configurator camera is stripped per §14), so
        // Camera.main alone isn't reliable there.
        internal static Camera FindCamera()
        {
            // Parent to the same camera OutfitCapture renders (Camera.main) so the quads stay aligned
            // in the capture. Fall back to the studio PreviewCamera / highest-depth enabled camera
            // (e.g. before the Configurator camera is stripped per §14, or if the tag is missing).
            var scene = SceneManager.GetActiveScene();
            var main = Camera.main;
            if (main != null && main.gameObject.scene == scene && main.isActiveAndEnabled) return main;

            Camera best = null;
            foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (cam.gameObject.scene != scene || !cam.isActiveAndEnabled) continue;
                if (cam.name == "PreviewCamera") return cam;
                if (best == null || cam.depth > best.depth) best = cam;
            }
            return best;
        }

        // --- EditorPrefs helpers -----------------------------------------------------------------

        private static Color Hex(string s) => ColorUtility.TryParseHtmlString(s, out var c) ? c : Color.magenta;

        private static Color GetColor(string key, Color def, bool keepAlpha = false)
        {
            var s = EditorPrefs.GetString(key, null);
            if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c))
                return keepAlpha ? c : new Color(c.r, c.g, c.b, def.a);
            return def;
        }

        private static void SetColor(string key, Color value)
        {
            EditorPrefs.SetString(key, "#" + ColorUtility.ToHtmlStringRGBA(value));
            Refresh();
        }

        private static void SetFloat(string key, float value)
        {
            EditorPrefs.SetFloat(key, value);
            Refresh();
        }
    }
}
