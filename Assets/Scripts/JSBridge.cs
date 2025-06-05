using System;
using JetBrains.Annotations;
using UnityEngine;

public class JSBridge : MonoBehaviour
{
    [SerializeField] private Bootstrap bootstrap;

    [UsedImplicitly]
    public void ParseFromURL()
    {
        bootstrap.ParseFromURL();
        bootstrap.Reload(); // TODO: Await this somehow?
    }

    [UsedImplicitly]
    public void SetProfile(string value)
    {
        bootstrap.Config.Profile = value;
    }

    [UsedImplicitly]
    public void SetEmote(string value)
    {
        bootstrap.Config.Emote = value;
    }

    [UsedImplicitly]
    public void SetBase64(string value)
    {
        bootstrap.Config.Base64 = Convert.FromBase64String(value);
    }

    [UsedImplicitly]
    public void SetUrn(string value)
    {
        bootstrap.Config.Urn = value;
    }

    [UsedImplicitly]
    public void SetBackground(string value)
    {
        bootstrap.Config.Background = ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color
            : Color.black;
    }

    [UsedImplicitly]
    public void SetSkinColor(string value)
    {
        bootstrap.Config.SkinColor = ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color
            : Color.black;
    }

    [UsedImplicitly]
    public void SetHairColor(string value)
    {
        bootstrap.Config.HairColor = ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color
            : Color.black;
    }

    [UsedImplicitly]
    public void SetEyeColor(string value)
    {
        bootstrap.Config.EyeColor = ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color
            : Color.black;
    }

    [UsedImplicitly]
    public void SetBodyShape(string value)
    {
        bootstrap.Config.BodyShape = value;
    }

    [UsedImplicitly]
    public void SetProjection(string value)
    {
        bootstrap.Config.Projection = value;
    }

    [UsedImplicitly]
    public void SetContract(string value)
    {
        bootstrap.Config.Contract = value;
    }

    [UsedImplicitly]
    public void SetItemID(string value)
    {
        bootstrap.Config.ItemID = value;
    }

    [UsedImplicitly]
    public void SetTokenID(string value)
    {
        bootstrap.Config.TokenID = value;
    }
}