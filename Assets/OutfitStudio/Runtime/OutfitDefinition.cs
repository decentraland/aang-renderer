using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Runtime.Wearables;
using UnityEngine;

namespace OutfitStudio
{
    /// <summary>
    /// A reproducible outfit: body shape, one wearable URN per slot, avatar colors and a pose.
    ///
    /// The share code is the renderer's own builder-mode query string, so the same code loads
    /// identically in the Outfit Studio, in <c>Bootstrap.debugUrl</c> and in the deployed
    /// web renderer (see <see cref="AangConfiguration.RecreateFrom"/>).
    /// </summary>
    [Serializable]
    public class OutfitDefinition
    {
        public string bodyShape = WearablesConstants.BODY_SHAPE_MALE;
        public List<string> urns = new();

        public Color skinColor = new(0.8f, 0.61f, 0.46f);
        public Color hairColor = new(0.23f, 0.13f, 0.09f);
        public Color eyeColor = new(0.23f, 0.14f, 0.05f);

        /// <summary>Embedded emote name (idle, clap, dance, ...) or an emote URN.</summary>
        public string emote = "idle";

        /// <summary>
        /// Draft (unpublished Builder) items as base64-encoded RawActiveEntity JSONs — the same
        /// format as the renderer's base64 query param, so share codes stay compatible.
        /// May include one emote, which takes pose priority in builder mode.
        /// </summary>
        public List<string> base64Items = new();

        /// <summary>
        /// Categories that render even when another equipped wearable's <c>hides</c>/<c>replaces</c>
        /// list would suppress them — the same mechanism as a profile's <c>forceRender</c>. Studio-only:
        /// the renderer's query string has no <c>forceRender</c> parameter, so this cannot travel in a
        /// share code and only survives in a preset (see <see cref="ToShareCode"/>).
        /// </summary>
        public List<string> forceRender = new();

        /// <summary>
        /// Force-renders every category at once, so no wearable can hide another. Kept separate from
        /// <see cref="forceRender"/> so toggling it off restores the individual picks underneath.
        /// </summary>
        public bool ignoreAllHides;

        /// <summary>
        /// Single-Item mode: render one isolated wearable with the body suppressed, for item-card
        /// beauty shots. The item still loads onto the live avatar skeleton (so it poses, skins and
        /// spring-bones normally) — only the body geometry is hidden. Studio-only, like
        /// <see cref="forceRender"/>: the renderer's query string has no hide-body parameter, so this
        /// survives in a preset but not in a share code (see <see cref="ToShareCode"/>).
        /// </summary>
        public bool soloItem;

        /// <summary>
        /// The isolated item's URN. Deliberately separate from <see cref="urns"/> so switching back to
        /// avatar mode leaves the outfit that was being authored untouched.
        /// </summary>
        public string soloUrn;

        /// <summary>A builder draft as the isolated item, base64-encoded like <see cref="base64Items"/>.</summary>
        public string soloBase64;

        /// <summary>
        /// How tightly the item fills the frame (or the card, with <see cref="soloFitToCard"/>), as a
        /// **percentage**: 100 means it exactly touches the rect on whichever axis binds, 200 makes it
        /// twice that size and crop, 50 half. It's a plain scale on the fitted distance, which is what
        /// "zoom" means to this tool's audience (§20).
        ///
        /// Replaced the per-axis <c>soloMarginXPx</c>/<c>soloMarginYPx</c> pair on 2026-08-04: only one of
        /// the two ever did anything on a given item (the bound axis sets the distance and the other margin
        /// just falls out of the aspect ratio), so two sliders read as two controls while behaving as one
        /// zoom with a trap in it. Per-item rather than an EditorPref because a long staff and an earring
        /// want different framing, so a preset should carry it.
        /// </summary>
        public float soloZoomPct = 100f;

        /// <summary>
        /// Vertical nudge in capture pixels, **positive = down**, following image-editor / Figma pixel
        /// coordinates rather than Unity's Y-up — this tool's audience thinks in the former (§20).
        ///
        /// Defaults to 70 because items land systematically high otherwise: measured on a capture of a
        /// jacket, 128 px of space above against 243 px below, i.e. ~58 px high. The geometric centre of
        /// an item's bounds sits below where the eye reads its centre, so this is a deliberate bias
        /// correction rather than a fudge — but the underlying reason has not been confirmed in-editor,
        /// so treat the exact value as tuned, not derived.
        /// </summary>
        public float soloOffsetYPx = 70f;

        /// <summary>
        /// Horizontal nudge in capture pixels, **positive = right**, the other half of image-editor pixel
        /// coordinates (see <see cref="soloOffsetYPx"/>). Defaults to 0: unlike the vertical axis there is
        /// no systematic bias to correct, since an item's bounds are horizontally centred on the avatar's
        /// vertical axis. It exists for the asymmetric cases — a single earring, a staff held to one side.
        /// </summary>
        public float soloOffsetXPx;

        /// <summary>
        /// Fit the item to the card rect instead of the whole frame. Off by default: the common job is
        /// rendering the item tight and large to composite a card around it in Photoshop, and the frame
        /// is what bounds that. Turn it on to compose the shot inside the studio's own card frame.
        /// </summary>
        public bool soloFitToCard;

        /// <summary>
        /// Measure only the garment, ignoring bare skin (arms, hands), so it reads at the same size in
        /// every pose. Off by default because it lets extremities overspill the margin and crop; useful
        /// when several items have to look consistent across a sheet.
        /// </summary>
        public bool soloFitGarmentOnly;

        /// <summary>Whether Single-Item mode has an item to render.</summary>
        public bool HasSoloItem =>
            soloItem && (!string.IsNullOrEmpty(soloUrn) || !string.IsNullOrEmpty(soloBase64));

        /// <summary>
        /// The force-render list to hand to the avatar loader: every known category when
        /// <see cref="ignoreAllHides"/> is set, otherwise the explicit per-slot picks.
        ///
        /// Note this is never null — an empty array and null are NOT equivalent inside
        /// <c>WearableUtils.ResolveHidingConflicts</c>, and empty is what the runtime passes for a
        /// profile with no force-render set, which is the behaviour the studio should match.
        ///
        /// Single-Item mode also force-renders everything: only one wearable is equipped, so there is
        /// nothing legitimate for a hide to suppress, and an item whose own category is implicitly
        /// hidden (a skin, a helmet hiding hair) would otherwise render as nothing at all.
        /// </summary>
        public string[] EffectiveForceRender() =>
            ignoreAllHides || soloItem
                ? AllCategories()
                : forceRender.ToArray();

        private static string[] AllCategories() =>
            WearableCategories.CATEGORIES_PRIORITY
                .Union(WearableCategories.SKIN_IMPLICIT_CATEGORIES)
                .ToArray();

        /// <summary>
        /// The URNs to load: just the isolated item in Single-Item mode, the whole outfit otherwise.
        /// </summary>
        public List<string> EffectiveUrns() =>
            soloItem
                ? string.IsNullOrEmpty(soloUrn) ? new List<string>() : new List<string> { soloUrn }
                : urns;

        /// <summary>
        /// The base64 draft items to load, following the same Single-Item substitution as
        /// <see cref="EffectiveUrns"/>. A draft emote in <see cref="base64Items"/> is deliberately
        /// dropped in Single-Item mode — the pose comes from <see cref="emote"/> there.
        /// </summary>
        public List<string> EffectiveBase64Items() =>
            soloItem
                ? string.IsNullOrEmpty(soloBase64) ? new List<string>() : new List<string> { soloBase64 }
                : base64Items;

        public OutfitDefinition Clone()
        {
            return new OutfitDefinition
            {
                bodyShape = bodyShape,
                urns = new List<string>(urns),
                skinColor = skinColor,
                hairColor = hairColor,
                eyeColor = eyeColor,
                emote = emote,
                base64Items = new List<string>(base64Items),
                forceRender = new List<string>(forceRender),
                ignoreAllHides = ignoreAllHides,
                soloItem = soloItem,
                soloUrn = soloUrn,
                soloBase64 = soloBase64,
                soloZoomPct = soloZoomPct,
                soloOffsetYPx = soloOffsetYPx,
                soloOffsetXPx = soloOffsetXPx,
                soloFitToCard = soloFitToCard,
                soloFitGarmentOnly = soloFitGarmentOnly
            };
        }

        /// <summary>
        /// A copy for saving into an <see cref="OutfitPreset"/>, with **every Single-Item field left at its
        /// default**. A preset is an avatar look; a solo item shot is temporary working state on the way to
        /// a PNG, which is why the Presets and Share code sections are hidden in that mode at all
        /// (<c>OutfitStudioWindow.RefreshSubject</c>). Without this, a preset saved from the avatar tab
        /// would still carry whichever item happened to be in the solo slot plus its framing, and loading
        /// it would overwrite someone else's scratch work — invisibly, since nothing in the avatar tab
        /// shows those values.
        ///
        /// Deliberately a separate method rather than making <see cref="Clone"/> lossy: cloning should mean
        /// cloning, even though presets are currently its only caller. The defaults come from a fresh
        /// instance rather than repeated literals, so tuning a field initializer above can't leave a stale
        /// copy of the old value down here.
        /// </summary>
        public OutfitDefinition CloneForPreset()
        {
            var copy = Clone();
            var defaults = new OutfitDefinition();

            copy.soloItem = defaults.soloItem;
            copy.soloUrn = defaults.soloUrn;
            copy.soloBase64 = defaults.soloBase64;
            copy.soloZoomPct = defaults.soloZoomPct;
            copy.soloOffsetYPx = defaults.soloOffsetYPx;
            copy.soloOffsetXPx = defaults.soloOffsetXPx;
            copy.soloFitToCard = defaults.soloFitToCard;
            copy.soloFitGarmentOnly = defaults.soloFitGarmentOnly;

            return copy;
        }

        /// <summary>Base64 decode tolerating missing padding (mirrors AangConfiguration.AddBase64).</summary>
        public static byte[] DecodeBase64(string value)
        {
            var sanitized = (value.Length % 4) switch
            {
                2 => value + "==",
                3 => value + "=",
                0 => value,
                _ => throw new FormatException("Invalid Base64 string")
            };

            return Convert.FromBase64String(sanitized);
        }

        public string ToShareCode()
        {
            var sb = new StringBuilder("?mode=builder");

            sb.AppendFormat("&bodyShape={0}", bodyShape);

            // The subject, not necessarily the whole outfit: in Single-Item mode this is the one item,
            // so a shared code shows the same wearable the studio is framing (worn on a visible body —
            // see the soloItem note below).
            foreach (var urn in EffectiveUrns())
                sb.AppendFormat("&urn={0}", urn);

            sb.AppendFormat("&skinColor={0}", ColorUtility.ToHtmlStringRGB(skinColor));
            sb.AppendFormat("&hairColor={0}", ColorUtility.ToHtmlStringRGB(hairColor));
            sb.AppendFormat("&eyeColor={0}", ColorUtility.ToHtmlStringRGB(eyeColor));

            if (!string.IsNullOrEmpty(emote) && emote != "idle")
                sb.AppendFormat("&emote={0}", emote);

            // Escaped because base64 may contain '+', which HttpUtility.UrlDecode
            // (used by AangConfiguration.RecreateFrom) would turn into a space
            foreach (var base64 in EffectiveBase64Items())
                sb.AppendFormat("&base64={0}", Uri.EscapeDataString(base64));

            // forceRender/ignoreAllHides are deliberately NOT emitted: AangConfiguration.RecreateFrom
            // has no parameter for them, so a code carrying one would load with the hides back on and
            // silently differ from the studio. The window warns while an override is active.
            //
            // soloItem and the framing fields likewise have no query parameter — a shared code loads the item on a
            // fully visible avatar, framed by the renderer's normal camera. Same trade-off, same warning.

            return sb.ToString();
        }

        /// <summary>Whether any hide override is active (so the UI can warn that share codes drop it).</summary>
        public bool HasForceRenderOverrides => ignoreAllHides || forceRender.Count > 0;

        /// <summary>Whether anything studio-only is active, so the UI can warn share codes drop it.</summary>
        public bool HasStudioOnlyState => HasForceRenderOverrides || soloItem;

        /// <summary>
        /// Parses a share code (or any renderer URL containing one). Unknown parameters are
        /// ignored so full web-renderer URLs can be pasted directly.
        /// </summary>
        public static OutfitDefinition FromShareCode(string code)
        {
            var outfit = new OutfitDefinition();

            if (string.IsNullOrWhiteSpace(code)) return outfit;

            var queryStart = code.IndexOf('?');
            var query = queryStart >= 0 ? code[(queryStart + 1)..] : code;

            foreach (var parameter in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var keyValueSplit = parameter.Split('=');
                var key = Uri.UnescapeDataString(keyValueSplit[0]).Trim();
                var value = (keyValueSplit.Length > 1 ? Uri.UnescapeDataString(keyValueSplit[1]) : string.Empty).Trim();

                switch (key)
                {
                    case "bodyShape":
                        outfit.bodyShape = value;
                        break;
                    case "urn":
                        outfit.urns.Add(value);
                        break;
                    case "skinColor":
                        if (ColorUtility.TryParseHtmlString("#" + value, out var skin)) outfit.skinColor = skin;
                        break;
                    case "hairColor":
                        if (ColorUtility.TryParseHtmlString("#" + value, out var hair)) outfit.hairColor = hair;
                        break;
                    case "eyeColor":
                        if (ColorUtility.TryParseHtmlString("#" + value, out var eye)) outfit.eyeColor = eye;
                        break;
                    case "emote":
                        outfit.emote = value;
                        break;
                    case "base64":
                        outfit.base64Items.Add(value);
                        break;
                }
            }

            return outfit;
        }
    }
}
