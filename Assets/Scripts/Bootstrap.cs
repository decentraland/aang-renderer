using Configurator;
using DCL.Rendering.DCL_Toon;
using GLTFast;
using Preview;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private Material baseMat;
    [SerializeField] private Material facialFeaturesMat;

    [Tooltip("Shared matcap library (MatcapPresets.asset from the unity-shared-dependencies package).")]
    [SerializeField] private MatcapPresets matcapPresets;

    [Tooltip("Preset name bound by default to metallic materials. Must match an entry in matcapPresets.")]
    [SerializeField] private string defaultMatcapName = "matcap_01";

    [SerializeField] private ConfiguratorController configuratorController;
    [SerializeField] private PreviewController previewController;

    [SerializeField, TextArea] private string debugUrl;

    private void Start()
    {
        // Common assets
        CommonAssets.AvatarMaterial = baseMat;
        CommonAssets.FacialFeaturesMaterial = facialFeaturesMat;
        CommonAssets.MatcapPresets = matcapPresets;
        CommonAssets.DefaultMatcapName = defaultMatcapName;

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

    [ContextMenu("Reload")]
    private void Reload()
    {
        var bridge = FindAnyObjectByType<JSBridge>();
        bridge.Reload();
    }

    [ContextMenu("Set Name")]
    private void SetName()
    {
        var bridge = FindAnyObjectByType<JSBridge>();
        bridge.SetUsername("Miha");
    }

}
