using System.Collections.Generic;
using System.Reflection;
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

    [SerializeField, TextArea] private string debugUrl;

    private void Start()
    {
        RegisterDclGltfExtensions();

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

    /// <summary>
    /// Registers glTF extensions that GLTFast doesn't natively support but that are safe
    /// to ignore (they don't affect geometry). Without this, GLTFast rejects any file that
    /// lists these in extensionsRequired.
    /// </summary>
    private static void RegisterDclGltfExtensions()
    {
        var field = typeof(GltfImport).GetField("k_SupportedExtensions",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (field == null)
        {
            Debug.LogWarning("Could not find k_SupportedExtensions field in GltfImport. " +
                             "Spring bone wearables may fail to load.");
            return;
        }

        var extensions = (HashSet<string>)field.GetValue(null);
        extensions.Add("DCL_spring_bone_joint");
    }
}
