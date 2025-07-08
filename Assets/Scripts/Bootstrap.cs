using System;
using GLTFast;
using UI;
using UnityEngine;
using Utils;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PreviewLoader previewLoader;
    [SerializeField] private Material baseMat;
    [SerializeField] private Material facialFeaturesMat;
    [SerializeField] private PreviewRotator previewRotator;
    [SerializeField] private UIPresenter uiPresenter;

    [SerializeField] private GameObject animationReference;
    [SerializeField] private GameObject authPlatform;

    public PreviewConfiguration Config;

    private bool _loading;
    private bool _shouldReload;
    private bool _shouldCleanup;

    // ReSharper disable once AsyncVoidMethod
    private async void Start()
    {
        // Common assets
        CommonAssets.AvatarMaterial = baseMat;
        CommonAssets.FacialFeaturesMaterial = facialFeaturesMat;

        // Sets uninterrupted defer agent for fastest loading
        GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());

        // Let's make it a bit smoother
        Application.targetFrameRate = 60;

        // Disable logging in release builds
        Debug.unityLogger.logEnabled = Debug.isDebugBuild;

        ParseFromURL();

        await Reload();
    }

    public void ParseFromURL(string url = null)
    {
        Config = URLParser.Parse(url ?? Application.absoluteURL);
    }

    public void InvokeReload()
    {
        _shouldCleanup = false;
        StartCoroutine(Reload());
    }

    private async Awaitable Reload()
    {
        if (_loading)
        {
            _shouldReload = true;
            return;
        }

        uiPresenter.ShowLoader(true);
        _loading = true;
        mainCamera.cullingMask = 0; // Render nothing

        do
        {
            _shouldReload = false;

            previewRotator.enabled = false;
            previewRotator.ResetRotation();

            animationReference.SetActive(Config.ShowAnimationReference);
            authPlatform.SetActive(Config.Mode is PreviewMode.Authentication);
            mainCamera.backgroundColor = Config.Background;
            mainCamera.orthographic = Config.Projection == "orthographic";
            uiPresenter.EnableLoader(!Config.DisableLoader);
            mainCamera.GetComponent<CameraController>().SetMode(Config.Mode);

            try
            {
                await previewLoader.LoadPreview(Config);
            }
            catch (Exception e)
            {
                JSBridge.NativeCalls.OnError(e.Message);
                throw;
            }
        } while (_shouldReload);

        // Wait for 1 frame for animation to kick in before re-centering the object on screen
        await Awaitable.NextFrameAsync();
        previewLoader.Recenter();

        previewRotator.enabled = true;
        previewRotator.AllowVertical = Config.Mode is PreviewMode.Marketplace or PreviewMode.Builder;
        previewRotator.EnableAutoRotate = Config.Mode is PreviewMode.Marketplace && !previewLoader.HasEmoteOverride;

        uiPresenter.EnableEmoteControls(previewLoader.HasEmoteOverride);
        uiPresenter.EnableZoom(Config.Mode is PreviewMode.Marketplace or PreviewMode.Builder);
        uiPresenter.EnableSwitcher(previewLoader.HasWearableOverride);
        uiPresenter.EnableAudioControls(previewLoader.HasEmoteAudio);

        var wantToShowAvatar = Config.Mode != PreviewMode.Marketplace || !previewLoader.HasWearableOverride ||
                               PlayerPrefs.GetInt("PreviewAvatarShown", 0) == 1;
        var canShowAvatar = Config.Mode != PreviewMode.Marketplace || previewLoader.HasValidRepresentation;

        uiPresenter.AllowAvatarSwitch(canShowAvatar);
        uiPresenter.ShowAvatar(!previewLoader.HasWearableOverride || wantToShowAvatar && canShowAvatar);

        uiPresenter.ShowLoader(false);
        _loading = false;
        mainCamera.cullingMask = -1; // Render everything

        if (_shouldCleanup)
        {
            Cleanup();
        }
        
        JSBridge.NativeCalls.OnLoadComplete();
    }

    public void Cleanup()
    {
        if (_loading)
        {
            _shouldCleanup = true;
            return;
        }

        _shouldCleanup = false;

        previewLoader.Cleanup();
    }
}