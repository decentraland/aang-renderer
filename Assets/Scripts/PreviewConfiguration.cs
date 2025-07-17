using System;
using System.Collections.Generic;
using UnityEngine;

public class PreviewConfiguration
{
    /// <summary>
    /// The mode in which the preview will run.
    /// </summary>
    public PreviewMode Mode { get; private set; } = PreviewMode.Profile;

    /// <summary>
    /// Converts the string value to the appropriate mode. Defaults to Marketplace.
    /// </summary>
    /// <param name="value"></param>
    public void SetMode(string value)
    {
        Mode = value switch
        {
            "profile" => PreviewMode.Profile,
            "authentication" => PreviewMode.Authentication,
            "builder" => PreviewMode.Builder,
            "configurator" => PreviewMode.Configurator,
            _ => PreviewMode.Marketplace
        };
    }

    /// <summary>
    /// An ethereum address of a profile to load as the base avatar.
    /// It can be set to default or a numbered default profile like default15 to use a default profile.
    /// </summary>
    public string Profile { get; set; } = "default1";

    /// <summary>
    /// The emote that the avatar will play. Default value is idle, other possible values are:
    /// clap, dab, dance, fashion, fashion-2, fashion-3,fashion-4, love, money, fist-pump and head-explode
    /// </summary>
    public string Emote { get; set; } = "idle";

    /// <summary>
    /// The base64 encoded GLB to load.
    /// </summary>
    public List<byte[]> Base64 { get; } = new();

    /// <summary>
    /// Converts the string base64 to a byte array and adds it to the list, adding padding if needed
    /// </summary>
    public void AddBase64(string value)
    {
        var sanitized = (value.Length % 4) switch
        {
            2 => value + "==",
            3 => value + "=",
            0 => value,
            _ => throw new FormatException("Invalid Base64 string")
        };

        Base64.Add(Convert.FromBase64String(sanitized));
    }

    /// <summary>
    /// A URN of a wearable or an emote to load. If it is a wearable, it will override anything loaded from a profile.
    /// </summary>
    public List<string> Urns { get; set; } = new();

    /// <summary>
    /// The color of the background in HEX.
    /// </summary>
    public Color Background { get; private set; } = new(0, 0, 0, 0);

    /// <summary>
    /// Sets the background color from a hex string. The string must not contain a leading #.
    /// </summary>
    public void SetBackground(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out var color))
        {
            Background = color;
        }
        else
        {
            Background = Color.black;
            Debug.LogError("Failed to parse background color");
        }
    }

    /// <summary>
    /// The color of the skin in HEX.
    /// </summary>
    public Color? SkinColor { get; private set; }

    /// <summary>
    /// Sets the skin color from a hex string. The string must not contain a leading #.
    /// </summary>
    public void SetSkinColor(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out var color))
        {
            SkinColor = color;
        }
        else
        {
            SkinColor = null;
            Debug.LogError("Failed to parse skin color");
        }
    }

    /// <summary>
    /// The color of the hair in HEX.
    /// </summary>
    public Color? HairColor { get; private set; }

    /// <summary>
    /// Sets the skin color from a hex string. The string must not contain a leading #.
    /// </summary>
    public void SetHairColor(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out var color))
        {
            HairColor = color;
        }
        else
        {
            HairColor = null;
            Debug.LogError("Failed to parse hair color");
        }
    }

    /// <summary>
    /// The color of the eyes in HEX.
    /// </summary>
    public Color? EyeColor { get; private set; }

    /// <summary>
    /// Sets the skin color from a hex string. The string must not contain a leading #.
    /// </summary>
    public void SetEyeColor(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out var color))
        {
            EyeColor = color;
        }
        else
        {
            EyeColor = null;
            Debug.LogError("Failed to parse eye color");
        }
    }

    /// <summary>
    /// The body shape URN (urn:decentraland:off-chain:base-avatars:BaseMale or urn:decentraland:off-chain:base-avatars:BaseFemale)
    /// </summary>
    public string BodyShape { get; set; }

    /// <summary>
    /// Shows or hides the animation reference platform.
    /// </summary>
    public bool ShowAnimationReference { get; set; }

    /// <summary>
    /// If we're using orthographic projection.
    /// </summary>
    public string Projection { get; set; } = "perspective";

    /// <summary>
    /// The contract address of the wearable collection.
    /// </summary>
    public string Contract { get; set; }

    /// <summary>
    /// The id of the item in the collection.
    /// </summary>
    public string ItemID { get; set; }

    /// <summary>
    /// The id of the token (to preview a specific NFT).
    /// </summary>
    public string TokenID { get; set; }

    /// <summary>
    /// If the loading circle should be shown when loading content.
    /// </summary>
    public bool DisableLoader { get; set; }

    /// <summary>
    /// If we should instruct the browser to pre-fetch certain files. On by default.
    /// </summary>
    public bool UseBrowserPreload { get; set; } = true;
}

public enum PreviewMode
{
    Marketplace,
    Authentication,
    Profile,
    Builder,
    Configurator
}