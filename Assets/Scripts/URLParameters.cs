using UnityEngine;

public class URLParameters
{
    // Logged in

    /// <summary>
    /// An ethereum address of a profile to load as the base avatar.
    /// It can be set to default or a numbered default profile like default15 to use a default profile.
    /// </summary>
    public string Profile { get; private set; } = "default1"; // TODO: Handle default profile?
    
    /// <summary>
    /// The emote that the avatar will play. Default value is idle, other possible values are:
    /// clap, dab, dance, fashion, fashion-2, fashion-3,fashion-4, love, money, fist-pump and head-explode
    /// </summary>
    public string Emote { get; private set; }
    
    // Ethereum

    /// <summary>
    /// A URN of a wearable or an emote to load. If it is a wearable, it will override anything loaded from a profile.
    /// </summary>
    public string Urn { get; private set; }

    /// <summary>
    /// The color of the background in HEX.
    /// </summary>
    public Color Background { get; private set; } = new(75 / 255f, 118 / 255f, 219 / 255f); // TODO: Default?
    
    // Polygon
    
    /// <summary>
    /// The contract address of the wearable collection.
    /// </summary>
    public string Contract { get; private set; }
    
    /// <summary>
    /// The id of the item in the collection.
    /// </summary>
    public string ItemID { get; private set; }
    
    /// <summary>
    /// The id of the token (to preview a specific NFT).
    /// </summary>
    public string TokenID { get; private set; }
    
    private URLParameters() { }

    public static URLParameters ParseDefault() => Parse(Application.absoluteURL);

    public static URLParameters Parse(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.Contains('?')) return null;

        var split = url[(url.IndexOf('?') + 1)..].Split('&');
        
        if(split.Length == 0) return null;

        var parameters = new URLParameters();
        
        foreach (var parameter in split)
        {
            var keyValueSplit = parameter.Split('=');
            var key = keyValueSplit[0];
            var value = keyValueSplit[1];
            
            switch (key)
            {
                case "profile":
                    parameters.Profile = value;
                    break;
                case "emote":
                    parameters.Emote = value;
                    break;
                case "urn":
                    parameters.Urn = value;
                    break;
                case "background":
                    parameters.Background = ColorUtility.TryParseHtmlString("#" + value, out var color) ? color : Color.black;
                    break;
                case "contract":
                    parameters.Contract = value;
                    break;
                case "item":
                    parameters.ItemID = value;
                    break;
                case "token":
                    parameters.TokenID = value;
                    break;
                default:
                    Debug.LogWarning($"Unknown parameter in URL: {key}");
                    break;
            }
        }

        return parameters;
    }
}

