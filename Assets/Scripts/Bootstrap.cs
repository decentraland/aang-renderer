using GLTFast;
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

        ParseFromURL();

        await Reload();
    }

    public void ParseFromURL(string url = null)
    {
        Config = URLParser.Parse(url ?? Application.absoluteURL);
    }

    public void InvokeReload()
    {
        StartCoroutine(Reload());
    }

    public void InvokeLightReload()
    {
        animationReference.SetActive(Config.ShowAnimationReference);
        authPlatform.SetActive(Config.Mode is PreviewMode.Authentication);
        mainCamera.backgroundColor = Config.Background;
        mainCamera.orthographic = Config.Projection == "orthographic";
    }

    private async Awaitable Reload()
    {
        if (_loading)
        {
            _shouldReload = true;
            return;
        }

        uiPresenter.EnableLoader(true);
        _loading = true;

        do
        {
            _shouldReload = false;

            InvokeLightReload();

            await previewLoader.LoadPreview(Config);

            previewRotator.AllowVertical = Config.Mode is PreviewMode.Marketplace or PreviewMode.Builder;
            previewRotator.EnableAutoRotate = Config.Mode is PreviewMode.Marketplace && !previewLoader.HasEmoteOverride;
            previewRotator.ResetRotation();

            uiPresenter.EnableEmoteControls(previewLoader.HasEmoteOverride);
            uiPresenter.EnableZoom(Config.Mode is PreviewMode.Marketplace or PreviewMode.Builder);
            uiPresenter.EnableSwitcher(previewLoader.HasWearableOverride);
            uiPresenter.EnableAudioControls(previewLoader.HasEmoteAudio);

            if (Config.Mode is PreviewMode.Marketplace && previewLoader.HasWearableOverride)
            {
                uiPresenter.ShowAvatar(false);
            }
            
        } while (_shouldReload);

        uiPresenter.EnableLoader(false);
        _loading = false;
    }
}