using System;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Holds all the parameters passed in via the URL.
    /// </summary>
    public static class URLParser
    {
        public static PreviewConfiguration Parse(string url)
        {
            var config = new PreviewConfiguration();

            if (string.IsNullOrEmpty(url) || !url.Contains('?')) return config;

            var split = url[(url.IndexOf('?') + 1)..].Split('&');

            if (split.Length == 0) return config;


            foreach (var parameter in split)
            {
                var keyValueSplit = parameter.Split('=');
                var key = keyValueSplit[0];
                var value = keyValueSplit[1];

                switch (key)
                {
                    case "mode":
                        config.Mode = value switch
                        {
                            "profile" => PreviewConfiguration.PreviewMode.Profile,
                            "authentication" => PreviewConfiguration.PreviewMode.Authentication,
                            "builder" => PreviewConfiguration.PreviewMode.Builder,
                            _ => PreviewConfiguration.PreviewMode.Marketplace
                        };
                        break;
                    case "profile":
                        config.Profile = value;
                        break;
                    case "emote":
                        config.Emote = value;
                        break;
                    case "urn":
                        config.Urn = value;
                        break;
                    case "background":
                        config.Background = ColorUtility.TryParseHtmlString("#" + value, out var backgroundColor)
                            ? backgroundColor
                            : Color.black;
                        break;
                    case "skinColor":
                        config.SkinColor = ColorUtility.TryParseHtmlString("#" + value, out var skinColor)
                            ? skinColor
                            : null;
                        break;
                    case "hairColor":
                        config.HairColor = ColorUtility.TryParseHtmlString("#" + value, out var hairColor)
                            ? hairColor
                            : null;
                        break;
                    case "eyeColor":
                        config.EyeColor = ColorUtility.TryParseHtmlString("#" + value, out var eyesColor)
                            ? eyesColor
                            : null;
                        break;
                    case "bodyShape":
                        config.BodyShape = value;
                        break;
                    case "upperBody":
                        config.UpperBody = value;
                        break;
                    case "lowerBody":
                        config.LowerBody = value;
                        break;
                    case "projection":
                        config.Projection = value;
                        break;
                    case "base64":
                        config.Base64 = Convert.FromBase64String(value + "==");
                        break;
                    case "contract":
                        config.Contract = value;
                        break;
                    case "item":
                        config.ItemID = value;
                        break;
                    case "token":
                        config.TokenID = value;
                        break;
                    default:
                        Debug.LogWarning($"Unknown parameter in URL: {key}");
                        break;
                }
            }

            return config;
        }
    }
}