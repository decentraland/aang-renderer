using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/// <summary>
/// Used for interacting with the unity renderer from JavaScript.
///
/// Reload must be called manually for the changes to take effect.
///
/// Usage: unityInstance.SendMessage('JSBridge', 'MethodName', 'value');
/// </summary>
public class JSBridge : MonoBehaviour
{
    [SerializeField] private Bootstrap bootstrap;

    [UsedImplicitly]
    public void ParseFromURL() => bootstrap.ParseFromURL();

    [UsedImplicitly]
    public void SetMode(string value) => bootstrap.Config.SetMode(value);

    [UsedImplicitly]
    public void SetProfile(string value) => bootstrap.Config.Profile = value;

    [UsedImplicitly]
    public void SetEmote(string value) => bootstrap.Config.Emote = value;

    [UsedImplicitly]
    public void AddBase64(string value) => bootstrap.Config.AddBase64(value);

    [UsedImplicitly]
    public void ClearBase64(string value) => bootstrap.Config.Base64.Clear();

    [UsedImplicitly]
    public void SetUrns(string value) =>
        bootstrap.Config.Urns = value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    [UsedImplicitly]
    public void SetBackground(string value) => bootstrap.Config.SetBackground(value);

    [UsedImplicitly]
    public void SetSkinColor(string value) => bootstrap.Config.SetSkinColor(value);

    [UsedImplicitly]
    public void SetHairColor(string value) => bootstrap.Config.SetHairColor(value);

    [UsedImplicitly]
    public void SetEyeColor(string value) => bootstrap.Config.SetEyeColor(value);

    [UsedImplicitly]
    public void SetBodyShape(string value) => bootstrap.Config.BodyShape = value;

    [UsedImplicitly]
    public void SetShowAnimationReference(string value) => bootstrap.Config.ShowAnimationReference = bool.Parse(value);

    [UsedImplicitly]
    public void SetProjection(string value) => bootstrap.Config.Projection = value;

    [UsedImplicitly]
    public void SetContract(string value) => bootstrap.Config.Contract = value;

    [UsedImplicitly]
    public void SetItemID(string value) => bootstrap.Config.ItemID = value;

    [UsedImplicitly]
    public void SetTokenID(string value) => bootstrap.Config.TokenID = value;

    [UsedImplicitly]
    public void SetDisableLoader(string value) => bootstrap.Config.DisableLoader = bool.Parse(value);

    [UsedImplicitly]
    public void Reload() => bootstrap.InvokeReload();

    [UsedImplicitly]
    public void Cleanup() => bootstrap.Cleanup();

    [UsedImplicitly]
    public void TakeScreenshot() => StartCoroutine(TakeScreenshotCoroutine());

    [UsedImplicitly]
    public void EnableSound() => bootstrap.EnableSound();

    [UsedImplicitly]
    public void DisableSound() => bootstrap.DisableSound();

    [UsedImplicitly]
    public void Cleanup() => bootstrap.Cleanup();

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

        public static void OnError(string message) => Debug.LogError($"NativeCall OnError({message})");
#else
        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnScreenshotTaken(string base64Str);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnLoadComplete();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnError(string message);
#endif
    }
}