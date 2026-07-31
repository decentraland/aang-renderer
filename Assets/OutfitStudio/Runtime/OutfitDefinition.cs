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
        /// The force-render list to hand to the avatar loader: every known category when
        /// <see cref="ignoreAllHides"/> is set, otherwise the explicit per-slot picks.
        ///
        /// Note this is never null — an empty array and null are NOT equivalent inside
        /// <c>WearableUtils.ResolveHidingConflicts</c>, and empty is what the runtime passes for a
        /// profile with no force-render set, which is the behaviour the studio should match.
        /// </summary>
        public string[] EffectiveForceRender() =>
            ignoreAllHides
                ? WearableCategories.CATEGORIES_PRIORITY
                    .Union(WearableCategories.SKIN_IMPLICIT_CATEGORIES)
                    .ToArray()
                : forceRender.ToArray();

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
                ignoreAllHides = ignoreAllHides
            };
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

            foreach (var urn in urns)
                sb.AppendFormat("&urn={0}", urn);

            sb.AppendFormat("&skinColor={0}", ColorUtility.ToHtmlStringRGB(skinColor));
            sb.AppendFormat("&hairColor={0}", ColorUtility.ToHtmlStringRGB(hairColor));
            sb.AppendFormat("&eyeColor={0}", ColorUtility.ToHtmlStringRGB(eyeColor));

            if (!string.IsNullOrEmpty(emote) && emote != "idle")
                sb.AppendFormat("&emote={0}", emote);

            // Escaped because base64 may contain '+', which HttpUtility.UrlDecode
            // (used by AangConfiguration.RecreateFrom) would turn into a space
            foreach (var base64 in base64Items)
                sb.AppendFormat("&base64={0}", Uri.EscapeDataString(base64));

            // forceRender/ignoreAllHides are deliberately NOT emitted: AangConfiguration.RecreateFrom
            // has no parameter for them, so a code carrying one would load with the hides back on and
            // silently differ from the studio. The window warns while an override is active.

            return sb.ToString();
        }

        /// <summary>Whether any hide override is active (so the UI can warn that share codes drop it).</summary>
        public bool HasForceRenderOverrides => ignoreAllHides || forceRender.Count > 0;

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
