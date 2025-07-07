using System;
using UI;
using UnityEngine;
using Utils;

public class PreviewController: MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    [SerializeField] private PreviewRotator previewRotator;
    [SerializeField] private PreviewUIPresenter previewUIPresenter;
    [SerializeField] private PreviewLoader previewLoader;
    [SerializeField] private GameObject animationReference;
    [SerializeField] private GameObject authPlatform;

    public PreviewConfiguration Config;

    private bool _loading;
    private bool _shouldReload;
    private bool _shouldCleanup;

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

        previewUIPresenter.ShowLoader(true);
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
            previewUIPresenter.EnableLoader(!Config.DisableLoader);
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

            previewRotator.enabled = true;
            previewRotator.AllowVertical = Config.Mode is PreviewMode.Marketplace or PreviewMode.Builder;
            previewRotator.EnableAutoRotate = Config.Mode is PreviewMode.Marketplace && !previewLoader.HasEmoteOverride;

            previewUIPresenter.EnableEmoteControls(previewLoader.HasEmoteOverride);
            previewUIPresenter.EnableZoom(Config.Mode is PreviewMode.Marketplace or PreviewMode.Builder);
            previewUIPresenter.EnableSwitcher(previewLoader.HasWearableOverride);
            previewUIPresenter.EnableAudioControls(previewLoader.HasEmoteAudio);

            var wantToShowAvatar = Config.Mode != PreviewMode.Marketplace || !previewLoader.HasWearableOverride ||
                                   PlayerPrefs.GetInt("PreviewAvatarShown", 0) == 1;
            var canShowAvatar = Config.Mode != PreviewMode.Marketplace || previewLoader.HasValidRepresentation;
            
            previewUIPresenter.AllowAvatarSwitch(canShowAvatar);
            previewUIPresenter.ShowAvatar(!previewLoader.HasWearableOverride || wantToShowAvatar && canShowAvatar);
        } while (_shouldReload);

        // Wait for 1 frame for animation to kick in before re-centering the object on screen
        await Awaitable.NextFrameAsync();
        previewLoader.Recenter();

        previewUIPresenter.ShowLoader(false);
        _loading = false;
        mainCamera.cullingMask = -1; // Render everything

        if (_shouldCleanup)
        {
            Cleanup();
        }
        
        JSBridge.NativeCalls.OnLoadComplete();
    }

    public void ParseFromURL(string url = null)
    {
        Config = URLParser.Parse(url ?? Application.absoluteURL);
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