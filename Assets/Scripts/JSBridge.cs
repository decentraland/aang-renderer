using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

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
        bootstrap.InvokeLightReload();
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
    public void SetShowAnimationReference(string value)
    {
        bootstrap.Config.ShowAnimationReference = bool.Parse(value);
        bootstrap.InvokeLightReload();
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

    [UsedImplicitly]
    public void SetDisableLoader(string value)
    {
        bootstrap.Config.DisableLoader = bool.Parse(value);
        bootstrap.InvokeLightReload();
    }

    [UsedImplicitly]
    public void TakeScreenshot()
    {
        StartCoroutine(TakeScreenshotCoroutine());
    }

    private static async Awaitable TakeScreenshotCoroutine()
    {
        await Awaitable.EndOfFrameAsync();

        var width = Screen.width;
        var height = Screen.height;

        var rt = RenderTexture.GetTemporary(width, height, 0, GraphicsFormat.B8G8R8A8_UNorm);

        ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);

        var gpuReadbackRequest = await AsyncGPUReadback.RequestAsync(rt);

        if (gpuReadbackRequest.hasError)
        {
            Debug.LogError("Failed to capture screenshot");
            NativeCalls.OnScreenshotTaken(null);
            return;
        }

        var sourceData = gpuReadbackRequest.GetData<byte>();

        var texture = new Texture2D(width, height, TextureFormat.BGRA32, false);
        var destinationData = texture.GetRawTextureData<byte>();

        // We have to flip the pixels vertically because OpenGL reasons
        for (var i = 0; i < sourceData.Length; i += 4)
        {
            var arrayIndex = i / 4;
            var x = arrayIndex % width;
            var y = arrayIndex / width;
            var flippedY = (height - 1 - y);
            var flippedIndex = x + flippedY * width;

            destinationData[i] = sourceData[flippedIndex * 4];
            destinationData[i + 1] = sourceData[flippedIndex * 4 + 1];
            destinationData[i + 2] = sourceData[flippedIndex * 4 + 2];
            destinationData[i + 3] = sourceData[flippedIndex * 4 + 3];
        }

        var pngBytes = texture.EncodeToPNG();
        var base64Png = Convert.ToBase64String(pngBytes);

        NativeCalls.OnScreenshotTaken(base64Png);

        RenderTexture.ReleaseTemporary(rt);
    }

    public static class NativeCalls
    {
#if UNITY_EDITOR
        public static void OnScreenshotTaken(string base64Str) =>
            Debug.Log($"NativeCall OnScreenshotTaken({base64Str.Length} bytes)");

        public static void OnLoadComplete() => Debug.Log("NativeCall OnLoadComplete");
#else
        [DllImport("__Internal")]
        public static extern void OnScreenshotTaken(string base64Str);

        [DllImport("__Internal")]
        public static extern void OnLoadComplete();
#endif
    }
}