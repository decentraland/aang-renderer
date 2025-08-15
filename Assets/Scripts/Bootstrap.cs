using Configurator;
using GLTFast;
using Preview;
using UnityEngine;

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

        var url = Application.isEditor ? debugUrl : Application.absoluteURL;
        AangConfiguration.RecreateFrom(url);

        if (AangConfiguration.Instance.UninterruptedDeferAgent)
        {
            // Sets uninterrupted defer agent for fastest loading
            GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());
        }

        if (AangConfiguration.Instance.Mode == PreviewMode.Configurator)
        {
            configuratorController.gameObject.SetActive(true);
            previewController.gameObject.SetActive(false);
        }
        else
        {
            configuratorController.gameObject.SetActive(false);
            previewController.gameObject.SetActive(true);
        }
    }
}