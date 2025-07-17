using System;
using System.Text;
using Data;
using GLTFast;
using UnityEngine;
using Utils;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private Material baseMat;
    [SerializeField] private Material facialFeaturesMat;

    [SerializeField] private ConfiguratorController configuratorController;
    [SerializeField] private PreviewController previewController;

    [SerializeField] private string debugUrl;

    private void Start()
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

        var url = Application.isEditor ? debugUrl : Application.absoluteURL;
        var initialConfig = URLParser.Parse(url);

        if (initialConfig.Mode == PreviewMode.Configurator)
        {
            // TODO: Change this to instantiate before release!
            configuratorController.gameObject.SetActive(true);
            previewController.gameObject.SetActive(false);

            configuratorController.UseBrowserPreload = initialConfig.UseBrowserPreload;
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
