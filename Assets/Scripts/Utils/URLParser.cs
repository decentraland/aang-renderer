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
                        config.SetMode(value);
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
                        config.SetBackground(value);
                        break;
                    case "skinColor":
                        config.SetSkinColor(value);
                        break;
                    case "hairColor":
                        config.SetHairColor(value);
                        break;
                    case "eyeColor":
                        config.SetEyeColor(value);
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
                    case "hair":
                        config.Hair = value;
                        break;
                    case "facialHair":
                        config.FacialHair = value;
                        break;
                    case "projection":
                        config.Projection = value;
                        break;
                    case "base64":
                        config.SetBase64(value);
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
                    case "env":
                        APIService.Environment = value == "dev" ? "zone" : "org";
                        Debug.Log($"Using environment {APIService.Environment}");
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