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
    [SerializeField] private PreviewController previewController;

    [UsedImplicitly]
    public void ParseFromURL() => previewController.ParseFromURL();

    [UsedImplicitly]
    public void SetMode(string value) => previewController.Config.SetMode(value);

    [UsedImplicitly]
    public void SetProfile(string value) => previewController.Config.Profile = value;

    [UsedImplicitly]
    public void SetEmote(string value) => previewController.Config.Emote = value;

    [UsedImplicitly]
    public void AddBase64(string value) => previewController.Config.AddBase64(value);

    [UsedImplicitly]
    public void ClearBase64(string value) => previewController.Config.Base64.Clear();

    [UsedImplicitly]
    public void SetUrns(string value) =>
        previewController.Config.Urns = value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    [UsedImplicitly]
    public void SetBackground(string value) => previewController.Config.SetBackground(value);

    [UsedImplicitly]
    public void SetSkinColor(string value) => previewController.Config.SetSkinColor(value);

    [UsedImplicitly]
    public void SetHairColor(string value) => previewController.Config.SetHairColor(value);

    [UsedImplicitly]
    public void SetEyeColor(string value) => previewController.Config.SetEyeColor(value);

    [UsedImplicitly]
    public void SetBodyShape(string value) => previewController.Config.BodyShape = value;

    [UsedImplicitly]
    public void SetShowAnimationReference(string value) => previewController.Config.ShowAnimationReference = bool.Parse(value);

    [UsedImplicitly]
    public void SetProjection(string value) => previewController.Config.Projection = value;

    [UsedImplicitly]
    public void SetContract(string value) => previewController.Config.Contract = value;

    [UsedImplicitly]
    public void SetItemID(string value) => previewController.Config.ItemID = value;

    [UsedImplicitly]
    public void SetTokenID(string value) => previewController.Config.TokenID = value;

    [UsedImplicitly]
    public void SetDisableLoader(string value) => previewController.Config.DisableLoader = bool.Parse(value);

    [UsedImplicitly]
    public void Reload() => previewController.InvokeReload();

    [UsedImplicitly]
    public void Cleanup() => previewController.Cleanup();

    [UsedImplicitly]
    public void TakeScreenshot() => StartCoroutine(TakeScreenshotCoroutine());

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
        
        public static void PreloadURLs(string urlsCSV) => Debug.Log($"NativeCall PreloadURLs({urlsCSV})");
#else
        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnScreenshotTaken(string base64Str);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnLoadComplete();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnError(string message);
        
        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void PreloadURLs(string urlsCSV);
#endif
    }
}