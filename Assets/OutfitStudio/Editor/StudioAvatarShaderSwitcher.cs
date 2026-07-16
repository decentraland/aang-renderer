using System;
using DCL.Rendering.DCL_Toon;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutfitStudio.Editor
{
    public enum StudioShaderMode
    {
        DclToon = 0,
        DclToonStudio = 1,
        DclStylizedPbr = 2
    }

    public enum StudioKnobKind { Float, Color }

    /// <summary>
    /// A single art-direction control exposed in the window's shader tuning panel and pushed onto
    /// every avatar material of the active shader. These are global look knobs (rim, ambient,
    /// stylization) — deliberately NOT per-wearable identity data (textures, base color, gates).
    /// </summary>
    public sealed class StudioShaderKnob
    {
        public readonly string Label;
        public readonly string Property;
        public readonly int PropId;
        public readonly StudioKnobKind Kind;
        public readonly float Min, Max, Default;
        public readonly Color DefaultColor;
        public readonly string Tooltip;

        public StudioShaderKnob(string label, string property, float min, float max, float def, string tooltip = null)
        {
            Label = label;
            Property = property;
            PropId = Shader.PropertyToID(property);
            Kind = StudioKnobKind.Float;
            Min = min;
            Max = max;
            Default = def;
            Tooltip = tooltip;
        }

        public StudioShaderKnob(string label, string property, Color def, string tooltip = null)
        {
            Label = label;
            Property = property;
            PropId = Shader.PropertyToID(property);
            Kind = StudioKnobKind.Color;
            DefaultColor = def;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Enforces the Outfit Studio's selected avatar shader on every avatar material, in edit AND
    /// play mode, across reloads, and pushes the art-direction tuning knobs onto them. Poll-based
    /// like the other studio helpers: every avatar reload creates fresh material clones with the
    /// stock DCL/DCL_Toon shader, and the next tick swaps them back to the selected shader and
    /// re-applies the tuning — no loader hooks needed.
    ///
    /// Swap notes: named properties survive material.shader reassignment, but renderQueue resets
    /// to the new shader's default (the generator sets it explicitly for cutout/transparent
    /// wearables) and keywords are restored defensively — both are saved around the swap.
    /// Facial features (DCL/DCL_Avatar_Facial_Features) are excluded by the shader-name filter.
    ///
    /// Also bootstraps CommonAssets.MatcapPresets (the metallic branch wires this in Bootstrap;
    /// the studio does it here to keep Bootstrap/Main.unity untouched).
    /// </summary>
    [InitializeOnLoad]
    public static class StudioAvatarShaderSwitcher
    {
        private const string EDITOR_PREFS_KEY = "OutfitStudio.Shader";
        private const string MATCAP_KEY = "OutfitStudio.Matcap";
        private const string MATCAP_PRESETS_PATH = "Assets/OutfitStudio/Shaders/Matcaps/MatcapPresets.asset";
        private const string DEFAULT_MATCAP_NAME = "matcap_01";

        private const string SHADER_TOON = "DCL/DCL_Toon";
        private const string SHADER_STUDIO = "DCL/DCL_Toon_Studio";
        private const string SHADER_PBR = "DCL/DCL_Stylized_PBR";

        private static double _nextCheck;
        private static string _warnedMissingShader;

        // Metal-gate diagnostic property ids (see the verbose dump in Apply).
        private static readonly int IsStylizedMetallicId = Shader.PropertyToID("_IsStylizedMetallic");
        private static readonly int MatcapArrId = Shader.PropertyToID("_MatCap_SamplerArr_ID");
        private static readonly int MatcapSamplerId = Shader.PropertyToID("_MatCap_Sampler");
        private static readonly int MetallicGlossArrId = Shader.PropertyToID("_MetallicGlossMapArr_ID");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int StylizedMetalStrengthId = Shader.PropertyToID("_StylizedMetalStrength");
        private static readonly int MatcapMetalBlendId = Shader.PropertyToID("_MatcapMetalBlend");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        // --- Tuning knobs (single source of truth: the window builds sliders from these) --------

        /// <summary>DCL_Toon_Studio — the knobs unlocked over the stock toon shader.</summary>
        public static readonly StudioShaderKnob[] StudioKnobs =
        {
            new("Rim Intensity", "_RimLightIntensity", 0f, 10f, 1f, "Overall strength of the rim/back light band."),
            new("Rim Power", "_RimLight_Power", 0f, 1f, 0.3f, "Rim falloff/width — higher wraps further onto the front."),
            new("Rim Inside Mask", "_RimLight_InsideMask", 0f, 0.95f, 0.15f, "Pushes the rim toward the silhouette edge."),
            new("Rim Color", "_RimLightColor", Color.white, "Rim tint (a cool blue reads as the Fortnite back light)."),
            new("Ambient (GI)", "_GI_Intensity", 0f, 2f, 0f, "Flat ambient fill from the environment SH."),
            new("Normal Strength", "_BumpScale", 0f, 2f, 1f, "Global normal-map intensity (overrides per-wearable scale)."),
            new("Metal Strength", "_StylizedMetalStrength", 0f, 1f, 1f, "Blend of the matcap metallic reflection."),
            new("Matcap Tint", "_MatCapColor", Color.white, "Colors the matcap metal reflection (white = untinted)."),
            new("Matcap Blur", "_BlurLevelMatcap", 0f, 4f, 0f, "Softens the matcap reflection (mip LOD).")
        };

        /// <summary>DCL_Stylized_PBR — the full principled + stylization control set.</summary>
        public static readonly StudioShaderKnob[] PbrKnobs =
        {
            new("Rim Intensity", "_RimLightIntensity", 0f, 4f, 1f, "Overall strength of the fresnel rim."),
            new("Rim Power", "_RimLight_Power", 0f, 1f, 0.3f, "Rim falloff/width."),
            new("Rim Inside Mask", "_RimLight_InsideMask", 0f, 0.95f, 0.15f, "Pushes the rim toward the silhouette edge."),
            new("Rim Sharpness", "_RimSharpness", 0f, 1f, 0f, "0 = soft gradient rim, 1 = hard band."),
            new("Rim Color", "_RimLightColor", Color.white, "Rim tint."),
            new("Diffuse Wrap", "_DiffuseWrap", 0f, 1f, 0.35f, "Wraps light past the terminator for softer shading."),
            new("Shadow Sharpness", "_ShadowSharpness", 0f, 1f, 0.35f, "0 = smooth lambert, 1 = hard two-tone break."),
            new("Specular Softness", "_SpecularSoftness", 0f, 4f, 0.5f, "Compresses the highlight into a broad stylized gleam."),
            new("Specular (F0)", "_Specular", 0f, 1f, 0.5f, "Dielectric reflectance (non-metal surfaces)."),
            new("Sheen", "_Sheen", 0f, 1f, 0f, "Cloth-like grazing-edge gleam."),
            new("Sheen Tint", "_SheenTint", 0f, 1f, 0.5f, "White vs albedo-tinted sheen."),
            new("Clearcoat", "_Clearcoat", 0f, 1f, 0f, "Glossy secondary coat (the action-figure finish)."),
            new("Clearcoat Gloss", "_ClearcoatGloss", 0f, 1f, 0.8f, "Sharpness of the clearcoat lobe."),
            new("Ambient (GI)", "_GI_Intensity", 0f, 5f, 1f, "Flat ambient fill from the environment SH."),
            new("Matcap Metal Blend", "_MatcapMetalBlend", 0f, 1f, 1f, "0 = physical edge-only reflection (dark front), 1 = flat matcap that matches DCL_Toon_Studio chrome."),
            new("Metal Strength", "_StylizedMetalStrength", 0f, 4f, 1f, "How strongly the matcap replaces the metal surface (1 = full, matches toon; >1 over-drives)."),
            new("Matcap Tint", "_MatCapColor", Color.white, "Colors the matcap metal reflection (white = untinted)."),
            new("Matcap Blur", "_BlurLevelMatcap", 0f, 4f, 0f, "Softens the matcap reflection (mip LOD)."),
            new("Normal Strength", "_BumpScale", 0f, 2f, 1f, "Global normal-map intensity (overrides per-wearable scale).")
        };

        public static StudioShaderKnob[] KnobsFor(StudioShaderMode mode) => mode switch
        {
            StudioShaderMode.DclToonStudio => StudioKnobs,
            StudioShaderMode.DclStylizedPbr => PbrKnobs,
            _ => Array.Empty<StudioShaderKnob>()
        };

        public static StudioShaderMode Mode
        {
            get => (StudioShaderMode)EditorPrefs.GetInt(EDITOR_PREFS_KEY, (int)StudioShaderMode.DclToon);
            set
            {
                EditorPrefs.SetInt(EDITOR_PREFS_KEY, (int)value);
                Apply(verbose: true); // user clicked a button — report the outcome
            }
        }

        /// <summary>
        /// The matcap preset (by name) bound to stylized-metal materials. Applies globally in the
        /// studio: newly generated materials pick it up via CommonAssets.DefaultMatcapName, and the
        /// poll pushes it onto every already-loaded metal material so a change is live. Persisted.
        /// </summary>
        public static string ActiveMatcapName
        {
            get => EditorPrefs.GetString(MATCAP_KEY, DEFAULT_MATCAP_NAME);
            set
            {
                EditorPrefs.SetString(MATCAP_KEY, value);
                CommonAssets.DefaultMatcapName = value; // future ApplyDefaultMatcap() calls use it
                Apply();
            }
        }

        /// <summary>Preset names from the loaded matcap library (empty until it's bootstrapped).</summary>
        public static string[] GetMatcapNames()
        {
            var presets = CommonAssets.MatcapPresets;
            if (presets == null || presets.Count == 0) return Array.Empty<string>();
            var names = new string[presets.Count];
            for (var i = 0; i < presets.Count; i++) names[i] = presets[i].name;
            return names;
        }

        static StudioAvatarShaderSwitcher()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += _ => EnsureMatcapPresets();
        }

        // --- Knob value storage (per shader mode; rim power for toon vs pbr are independent) -----

        private static string KnobKey(StudioShaderMode mode, StudioShaderKnob knob)
            => $"OutfitStudio.Knob.{(int)mode}.{knob.Property}";

        public static float GetFloat(StudioShaderMode mode, StudioShaderKnob knob)
            => EditorPrefs.GetFloat(KnobKey(mode, knob), knob.Default);

        public static Color GetColor(StudioShaderMode mode, StudioShaderKnob knob)
        {
            var s = EditorPrefs.GetString(KnobKey(mode, knob), null);
            if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c)) return c;
            return knob.DefaultColor;
        }

        public static void SetFloat(StudioShaderMode mode, StudioShaderKnob knob, float value)
        {
            EditorPrefs.SetFloat(KnobKey(mode, knob), value);
            Apply();
        }

        public static void SetColor(StudioShaderMode mode, StudioShaderKnob knob, Color value)
        {
            EditorPrefs.SetString(KnobKey(mode, knob), "#" + ColorUtility.ToHtmlStringRGBA(value));
            Apply();
        }

        public static void ResetKnobs(StudioShaderMode mode)
        {
            foreach (var knob in KnobsFor(mode))
                EditorPrefs.DeleteKey(KnobKey(mode, knob));
            Apply();
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 0.5;

            if (SceneManager.GetActiveScene().path != OutfitStudioWindow.STUDIO_SCENE_PATH) return;

            EnsureMatcapPresets();
            Apply();
        }

        public static void Apply() => Apply(false);

        /// <summary>
        /// Swaps every avatar material in the studio scene to the selected shader and pushes the
        /// tuning knobs onto it. Uses Resources.FindObjectsOfTypeAll so it reaches the edit-mode
        /// preview's HideFlags.DontSave renderers (FindObjectsByType would miss them) regardless of
        /// hierarchy, and covers play-mode wearables the same way; filtered to the active scene so
        /// project assets are never touched. Idempotent. When <paramref name="verbose"/>, logs the
        /// outcome (used on button clicks so a no-op is never silent).
        /// </summary>
        public static void Apply(bool verbose)
        {
            if (SceneManager.GetActiveScene().path != OutfitStudioWindow.STUDIO_SCENE_PATH)
            {
                if (verbose)
                    Debug.LogWarning("[OutfitStudio] Shader switching only runs in the studio scene " +
                                     $"({OutfitStudioWindow.STUDIO_SCENE_PATH}). Active scene is " +
                                     $"\"{SceneManager.GetActiveScene().path}\".");
                return;
            }

            var mode = Mode;
            var targetName = mode switch
            {
                StudioShaderMode.DclToonStudio => SHADER_STUDIO,
                StudioShaderMode.DclStylizedPbr => SHADER_PBR,
                _ => SHADER_TOON
            };

            var target = Shader.Find(targetName);
            if (target == null)
            {
                // Not imported yet, or a shader compile error left it unresolvable — surface it
                // once instead of silently doing nothing.
                if (_warnedMissingShader != targetName)
                {
                    Debug.LogWarning($"[OutfitStudio] Shader \"{targetName}\" not found — shader switch " +
                                     "skipped. Check the Console for shader compile errors, or reimport " +
                                     "Assets/OutfitStudio/Shaders.");
                    _warnedMissingShader = targetName;
                }
                return;
            }
            _warnedMissingShader = null;

            var knobs = KnobsFor(mode);
            var activeScene = SceneManager.GetActiveScene();

            // Resolve the selected matcap preset once — pushed onto every metal material below so the
            // window's matcap dropdown is live (the generator only binds it at material-creation time).
            var presets = CommonAssets.MatcapPresets;
            var haveMatcap = presets != null && presets.Count > 0;
            var activeMatcap = default(MatcapPresets.Preset);
            if (haveMatcap && !presets.TryGet(ActiveMatcapName, out activeMatcap))
                activeMatcap = presets[0];

            var avatarMats = 0;
            var swapped = 0;
            var metalDiag = verbose ? new System.Text.StringBuilder() : null;

            // Resources.FindObjectsOfTypeAll finds EVERY loaded renderer including HideFlags.DontSave
            // ones (the edit-mode preview uses that flag; FindObjectsByType would miss them) and
            // inactive ones — independent of avatar hierarchy. Filter to the studio scene so we never
            // touch project assets, prefab-stage objects, or other scenes.
            foreach (var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer.gameObject.scene != activeScene) continue;

                // sharedMaterials (never .material — that leaks instances in edit mode)
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null || mat.shader == null) continue;

                    var name = mat.shader.name;
                    if (name != SHADER_TOON && name != SHADER_STUDIO && name != SHADER_PBR) continue;

                    // Never touch persisted assets (Avatar_Toon.mat) — avatar materials are
                    // always runtime clones, so this only skips misconfigured edge cases.
                    if (EditorUtility.IsPersistent(mat)) continue;

                    avatarMats++;

                    if (name != targetName)
                    {
                        var queue = mat.renderQueue;
                        var keywords = mat.shaderKeywords;
                        mat.shader = target;
                        mat.shaderKeywords = keywords;
                        mat.renderQueue = queue;
                        swapped++;
                    }

                    // Push the current art-direction values (no-op for stock toon: it has no knobs)
                    foreach (var knob in knobs)
                    {
                        if (knob.Kind == StudioKnobKind.Float)
                            mat.SetFloat(knob.PropId, GetFloat(mode, knob));
                        else
                            mat.SetColor(knob.PropId, GetColor(mode, knob));
                    }

                    // Re-assert the stylized-metal gate flag. The material is born on the stock
                    // DCL/DCL_Toon package shader, which on this branch does NOT declare the
                    // metallic-branch property _IsStylizedMetallic. Setting a real Integer property
                    // the active shader doesn't declare doesn't survive the later mat.shader swap to
                    // the studio shader — it falls back to the shader default (0) — so the generator's
                    // _IsStylizedMetallic=1 is lost and the shader's `_IsStylizedMetallic > 0` gate
                    // never opens (metal invisible, though normals — never gated on it — still show).
                    // The mask id _MetallicGlossMapArr_ID DOES survive (>= 0 when the generator
                    // detected metal, -1 otherwise), so use it as the "metal was detected" signal and
                    // re-set the flag now that the active studio shader actually declares it.
                    if (mat.HasProperty(IsStylizedMetallicId) && mat.HasProperty(MetallicGlossArrId))
                        mat.SetInteger(IsStylizedMetallicId, mat.GetInteger(MetallicGlossArrId) >= 0 ? 1 : 0);

                    // Push the selected matcap TEXTURE onto metal materials (those the generator flagged
                    // with a mask, _MetallicGlossMapArr_ID >= 0) so the window's dropdown switches it
                    // live. Tint (_MatCapColor) and blur (_BlurLevelMatcap) are deliberately NOT set
                    // here — they're tuning knobs now (pushed by the knob loop above), so the preset
                    // only supplies the texture. Non-metal materials keep the gate shut, so they're
                    // left alone.
                    if (haveMatcap && mat.HasProperty(MatcapSamplerId) &&
                        mat.HasProperty(MetallicGlossArrId) && mat.GetInteger(MetallicGlossArrId) >= 0)
                    {
                        mat.SetTexture(MatcapSamplerId, activeMatcap.texture);
                        if (mat.HasProperty(MatcapArrId)) mat.SetInteger(MatcapArrId, 0);
                    }

                    // DIAGNOSTIC (verbose only): dump the metal-gate state per material so we can see
                    // which condition fails — detection (_IsStylizedMetallic), the matcap gate
                    // (_MatCap_SamplerArr_ID >= 0 + a bound _MatCap_Sampler), or the mask id.
                    // Every read is HasProperty-guarded — properties differ between the toon/PBR
                    // shaders, and an unguarded Get logs an error and returns 0.
                    if (metalDiag != null)
                    {
                        string I(int id) => mat.HasProperty(id) ? mat.GetInteger(id).ToString() : "n/a";
                        string F(int id) => mat.HasProperty(id) ? mat.GetFloat(id).ToString("0.##") : "n/a";
                        string T(int id) => !mat.HasProperty(id) ? "n/a" : (mat.GetTexture(id) != null ? "SET" : "null");
                        metalDiag.AppendLine(
                            $"    • {mat.name}: _IsStylizedMetallic={I(IsStylizedMetallicId)} " +
                            $"_MatCap_SamplerArr_ID={I(MatcapArrId)} " +
                            $"_MatCap_Sampler={T(MatcapSamplerId)} " +
                            $"_MetallicGlossMapArr_ID={I(MetallicGlossArrId)} " +
                            $"_MetallicGlossMap={T(MetallicGlossMapId)} " +
                            $"_StylizedMetalStrength={F(StylizedMetalStrengthId)} " +
                            $"_MatcapMetalBlend={F(MatcapMetalBlendId)} " +
                            $"_Metallic={F(MetallicId)}");
                    }
                }
            }

            if (verbose)
            {
                if (avatarMats == 0)
                    Debug.LogWarning($"[OutfitStudio] {targetName}: 0 avatar materials in the scene — " +
                                     "load an outfit into the preview (or enter play mode) first, then click again.");
                else
                    Debug.Log($"[OutfitStudio] {targetName}: {avatarMats} avatar material(s), {swapped} swapped.\n" +
                              $"Metal gate diagnostic (MatcapPresets={(CommonAssets.MatcapPresets != null ? CommonAssets.MatcapPresets.Count + " presets" : "NULL")}):\n" +
                              metalDiag);
            }

            if (swapped > 0)
            {
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        /// <summary>
        /// The metallic branch assigns the matcap library in Bootstrap; the studio assigns it here
        /// so the renderer's scene stays untouched. Runs cheaply on the poll (also after the
        /// play-mode domain reload wipes the statics).
        /// </summary>
        private static void EnsureMatcapPresets()
        {
            if (CommonAssets.MatcapPresets != null) return;

            CommonAssets.MatcapPresets = AssetDatabase.LoadAssetAtPath<MatcapPresets>(MATCAP_PRESETS_PATH);
            CommonAssets.DefaultMatcapName = ActiveMatcapName; // honor the window's selection for new materials
        }
    }
}
