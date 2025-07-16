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

        var base64 =
            "eyJpZCI6ImZjMTZhMjlmLTAxZjQtNDI5MC1iZTY5LThjNGQ1ZDFlZDZlZSIsIm5hbWUiOiJDaGVmZiBraXNzIiwidGh1bWJuYWlsIjoidGh1bWJuYWlsLnBuZyIsImltYWdlIjoidGh1bWJuYWlsLnBuZyIsImRlc2NyaXB0aW9uIjoiIiwiaTE4biI6W3siY29kZSI6ImVuIiwidGV4dCI6IkNoZWZmIGtpc3MifV0sImVtb3RlRGF0YUFEUjc0Ijp7ImNhdGVnb3J5Ijoic3R1bnQiLCJsb29wIjpmYWxzZSwidGFncyI6W10sInJlcHJlc2VudGF0aW9ucyI6W3siYm9keVNoYXBlcyI6WyJ1cm46ZGVjZW50cmFsYW5kOm9mZi1jaGFpbjpiYXNlLWF2YXRhcnM6QmFzZU1hbGUiXSwibWFpbkZpbGUiOiJtYWxlL2NoZWZmIGtpc3MuZ2xiIiwiY29udGVudHMiOlt7ImtleSI6Im1hbGUvY2hlZmYga2lzcy5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC56b25lL3YxL3N0b3JhZ2UvY29udGVudHMvYmFma3JlaWV0enN2anZrcG9uNWV5d25uYmRtdGdlaHo2czVtYWNxeGd1eDVidWh6aGFhZnNiM3F0eW0ifV19LHsiYm9keVNoYXBlcyI6WyJ1cm46ZGVjZW50cmFsYW5kOm9mZi1jaGFpbjpiYXNlLWF2YXRhcnM6QmFzZUZlbWFsZSJdLCJtYWluRmlsZSI6ImZlbWFsZS9jaGVmZiBraXNzLmdsYiIsImNvbnRlbnRzIjpbeyJrZXkiOiJmZW1hbGUvY2hlZmYga2lzcy5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC56b25lL3YxL3N0b3JhZ2UvY29udGVudHMvYmFma3JlaWV0enN2anZrcG9uNWV5d25uYmRtdGdlaHo2czVtYWNxeGd1eDVidWh6aGFhZnNiM3F0eW0ifV19XX19";

        var base64String = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        var metadata = JsonUtility.FromJson<ActiveEntity.Metadata>(base64String);
        
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
