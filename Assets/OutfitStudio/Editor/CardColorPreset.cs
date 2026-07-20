using UnityEngine;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// A named set of the 7 Card-frame colours (background top/bottom, glow, card top/bottom, border,
    /// bottom-fade). Editor-only tooling asset: author them via
    /// <c>Assets ▸ Create ▸ Outfit Studio ▸ Card Color Preset</c> (or the "Save current…" button in
    /// the Outfit Studio's Card-frame section) and apply them with the preset buttons there.
    ///
    /// Only colours are stored - margins, glow/fade sizes, corner radius, border width and the
    /// enable/side-mask toggles are intentionally left untouched so a preset re-skins the current
    /// layout instead of overwriting it. Lives in the Editor assembly, so nothing ships to a build.
    /// </summary>
    [CreateAssetMenu(fileName = "CardColorPreset", menuName = "Outfit Studio/Card Color Preset")]
    public class CardColorPreset : ScriptableObject
    {
        // Defaults mirror StudioCardFrame.Def* so a fresh preset matches the stock look.
        [ColorUsage(false)] public Color backgroundTop = new(0.0863f, 0.0784f, 0.2275f, 1f);
        [ColorUsage(false)] public Color backgroundBottom = new(0.2275f, 0.1176f, 0.3608f, 1f);
        [ColorUsage(true)] public Color glow = new(0.42f, 0.30f, 0.58f, 0.25f); // alpha is meaningful
        [ColorUsage(false)] public Color cardTop = new(0.4196f, 0.2471f, 0.6275f, 1f);
        [ColorUsage(false)] public Color cardBottom = new(0.2902f, 0.1569f, 0.4392f, 1f);
        [ColorUsage(false)] public Color border = new(0.7255f, 0.5490f, 0.8784f, 1f);
        [ColorUsage(false)] public Color bottomFade = new(0.2902f, 0.1569f, 0.4392f, 1f);
    }
}
