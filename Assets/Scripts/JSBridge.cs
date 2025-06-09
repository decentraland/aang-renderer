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
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetProfile(string value)
    {
        bootstrap.Config.Profile = value;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetEmote(string value)
    {
        bootstrap.Config.Emote = value;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetBase64(string value)
    {
        bootstrap.Config.Base64 = Convert.FromBase64String(value);
        bootstrap.InvokeReload();
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
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetSkinColor(string value)
    {
        bootstrap.Config.SkinColor = ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color
            : Color.black;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetHairColor(string value)
    {
        bootstrap.Config.HairColor = ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color
            : Color.black;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetEyeColor(string value)
    {
        bootstrap.Config.EyeColor = ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color
            : Color.black;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetBodyShape(string value)
    {
        bootstrap.Config.BodyShape = value;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetProjection(string value)
    {
        bootstrap.Config.Projection = value;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetContract(string value)
    {
        bootstrap.Config.Contract = value;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetItemID(string value)
    {
        bootstrap.Config.ItemID = value;
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetTokenID(string value)
    {
        bootstrap.Config.TokenID = value;
        bootstrap.InvokeReload();
    }
}