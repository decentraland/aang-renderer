using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Used for interacting with the unity renderer from JavaScript.
///
/// Usage: unityInstance.SendMessage('JSBridge', 'MethodName', 'value');
/// </summary>
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
    public void SetMode(string value)
    {
        bootstrap.Config.SetMode(value);
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
        bootstrap.Config.SetBase64(value);
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetUrns(string value)
    {
        bootstrap.Config.Urns = value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetBackground(string value)
    {
        bootstrap.Config.SetBackground(value);
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetSkinColor(string value)
    {
        bootstrap.Config.SetSkinColor(value);
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetHairColor(string value)
    {
        bootstrap.Config.SetHairColor(value);
        bootstrap.InvokeReload();
    }

    [UsedImplicitly]
    public void SetEyeColor(string value)
    {
        bootstrap.Config.SetEyeColor(value);
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