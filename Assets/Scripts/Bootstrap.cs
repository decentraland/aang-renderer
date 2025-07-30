using GLTFast;
using UnityEngine;
using Utils;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private Material baseMat;
    [SerializeField] private Material facialFeaturesMat;
    [SerializeField] private AnimationClip idleAnimation;

    [SerializeField] private ConfiguratorController configuratorController;
    [SerializeField] private PreviewController previewController;

    [SerializeField] private string debugUrl;

    private void Awake()
    {
        // Common assets
        CommonAssets.AvatarMaterial = baseMat;
        CommonAssets.FacialFeaturesMaterial = facialFeaturesMat;
        CommonAssets.IdleAnimation = idleAnimation;

        var url = Application.isEditor ? debugUrl : Application.absoluteURL;
        var initialConfig = URLParser.Parse(url);

        if (initialConfig.UninterruptedDeferAgent)
        {
            // Sets uninterrupted defer agent for fastest loading
            GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());
        }

        if (initialConfig.Mode == PreviewMode.Configurator)
        {
            // TODO: Change this to instantiate before release!
            configuratorController.gameObject.SetActive(true);
            previewController.gameObject.SetActive(false);

            configuratorController.Config = initialConfig;
        }
        else
        {
            configuratorController.gameObject.SetActive(false);
            previewController.gameObject.SetActive(true);

            previewController.Config = initialConfig;
            previewController.InvokeReload();
        }
    }
}