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

    public PreviewConfiguration Config;

    private bool _loading;
    private bool _shouldReload;

    // ReSharper disable once AsyncVoidMethod
    private async void Start()
    {
        // Common assets TODO: Improve maybe
        CommonAssets.AvatarMaterial = baseMat;
        CommonAssets.FacialFeaturesMaterial = facialFeaturesMat;

        // Sets uninterrupted defer agent for fastest loading
        GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());

        // Let's make it a bit smoother
        Application.targetFrameRate = 60;

        ParseFromURL();

        // Miha avatar
        // ParseFromURL("https://example.com/?mode=profile&profile=0x3f574d05ec670fe2c92305480b175654ca512005");
        // ParseFromURL("https://example.com/?mode=authentication&profile=0x3f574d05ec670fe2c92305480b175654ca512005");

        // Emote
        // ParseFromURL("https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&contract=0xb5e24ada4096b86ce3cf7af5119f19ed6089a80b&item=0");

        // Emote with prop
        // ParseFromURL("https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x97822560ec3e3522c1237f85817003211281eb79:0");

        // Emote with Audio
        ParseFromURL("https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0xb187264af67cf6d147521626203dedcfd901ceb3:4");
        
        // Builder TODO: Support base64
        // var base64Data = "eyJpZCI6IjUyNThhMmQxLTdiYTUtNGIwMi04MzY1LTg5MTZmMmUyMDgzZiIsIm5hbWUiOiJKYWNrZXQiLCJ0aHVtYm5h" +
        //                  "aWwiOiJ0aHVtYm5haWwucG5nIiwiaW1hZ2UiOiJ0aHVtYm5haWwucG5nIiwiZGVzY3JpcHRpb24iOiIiLCJpMThuIjpbeyJjb2Rl" +
        //                  "IjoiZW4iLCJ0ZXh0IjoiSmFja2V0In1dLCJkYXRhIjp7ImNhdGVnb3J5IjoidXBwZXJfYm9keSIsInJlcGxhY2VzIjpbXSwiaGlk" +
        //                  "ZXMiOltdLCJyZW1vdmVzRGVmYXVsdEhpZGluZyI6WyJoYW5kcyJdLCJ0YWdzIjpbXSwicmVwcmVzZW50YXRpb25zIjpbeyJib2R5U" +
        //                  "2hhcGVzIjpbInVybjpkZWNlbnRyYWxhbmQ6b2ZmLWNoYWluOmJhc2UtYXZhdGFyczpCYXNlTWFsZSJdLCJtYWluRmlsZSI6Im1hbGUv" +
        //                  "amFja2V0LmdsYiIsImNvbnRlbnRzIjpbeyJrZXkiOiJtYWxlL2phY2tldC5nbGIiLCJ1cmwiOiJodHRwOi8vbG9jYWxob3N0OjUwMDEv" +
        //                  "djEvc3RvcmFnZS9jb250ZW50cy9iYWZ5YmVpaHJleXNjYml3NXJkNXJ3anM1cHN6bGw1ZHFlb20zdWFxNXk3YXJ2YWF6a2hzZW11NHdka" +
        //                  "SJ9XSwib3ZlcnJpZGVIaWRlcyI6W10sIm92ZXJyaWRlUmVwbGFjZXMiOltdfSx7ImJvZHlTaGFwZXMiOlsidXJuOmRlY2VudHJhbGFuZDp" +
        //                  "vZmYtY2hhaW46YmFzZS1hdmF0YXJzOkJhc2VGZW1hbGUiXSwibWFpbkZpbGUiOiJmZW1hbGUvamFja2V0LmdsYiIsImNvbnRlbnRzIjpbeyJr" +
        //                  "ZXkiOiJmZW1hbGUvamFja2V0LmdsYiIsInVybCI6Imh0dHA6Ly9sb2NhbGhvc3Q6NTAwMS92MS9zdG9yYWdlL2NvbnRlbnRzL2JhZnliZWlocmV" +
        //                  "5c2NiaXc1cmQ1cndqczVwc3psbDVkcWVvbTN1YXE1eTdhcnZhYXpraHNlbXU0d2RpIn1dLCJvdmVycmlkZUhpZGVzIjpbXSwib3ZlcnJpZGVS" +
        //                  "ZXBsYWNlcyI6W119XSwicmVxdWlyZWRQZXJtaXNzaW9ucyI6W10sImJsb2NrVnJtRXhwb3J0IjpmYWxzZSwiaXNTbWFydCI6ZmFsc2V9fQ";
        // ParseFromURL($"https://example.com/?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=00ffff&hairColor=00ffff&skinColor=aaaaaa&upperBody=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&lowerBody=urn:decentraland:off-chain:base-avatars:kilt&base64={base64Data}");
        
        await Reload();
    }

    public void ParseFromURL(string url = null)
    {
        Config = URLParser.Parse(url ?? Application.absoluteURL);
    }

    private async Awaitable Reload()
    {
        _loading = true;
        mainCamera.backgroundColor = Config.Background;
        previewRotator.AllowVertical = Config.Mode is PreviewConfiguration.PreviewMode.Marketplace
            or PreviewConfiguration.PreviewMode.Builder;
        previewRotator.EnableAutoRotate = Config.Mode is PreviewConfiguration.PreviewMode.Marketplace;
        previewRotator.ResetRotation();
        await previewLoader.LoadPreview(Config);

        if (_shouldReload)
        {
            _shouldReload = false;
            await Reload();
        }
        
        _loading = false;
    }

    public void InvokeReload()
    {
        StartCoroutine(Reload());
    }
}