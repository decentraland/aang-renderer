using UnityEngine;

public class PreviewConfiguration
{
    public PreviewMode Mode { get; set; } = PreviewMode.Profile;

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
    public byte[] Base64 { get; set; } = null;

    /// <summary>
    /// A URN of a wearable or an emote to load. If it is a wearable, it will override anything loaded from a profile.
    /// </summary>
    public string Urn { get; set; }

    /// <summary>
    /// The color of the background in HEX.
    /// </summary>
    public Color Background { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// The color of the skin in HEX.
    /// </summary>
    public Color? SkinColor { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// The color of the hair in HEX.
    /// </summary>
    public Color? HairColor { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// The color of the eyes in HEX.
    /// </summary>
    public Color? EyeColor { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// The body shape URN (urn:decentraland:off-chain:base-avatars:BaseMale or urn:decentraland:off-chain:base-avatars:BaseFemale)
    /// </summary>
    public string BodyShape { get; set; } = null;
    
    public string Hair { get; set; }
    public string FacialHair { get; set; }
    public string UpperBody { get; set; }
    public string LowerBody{ get; set; }
    

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

    public enum PreviewMode
    {
        Marketplace,
        Authentication,
        Profile,
        Builder
    }
}