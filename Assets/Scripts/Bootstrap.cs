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

#if UNITY_EDITOR // Just so we don't accidentally break builds

        // Miha avatar
        // ParseFromURL("https://example.com/?mode=profile&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc");
        ParseFromURL("https://example.com/?mode=authentication&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc");

        // Emote
        // ParseFromURL("https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&contract=0xb5e24ada4096b86ce3cf7af5119f19ed6089a80b&item=0&background=039dfc");

        // Emote with prop
        // ParseFromURL("https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x97822560ec3e3522c1237f85817003211281eb79:0&background=039dfc");

        // Emote with Audio
        // ParseFromURL("https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0xb187264af67cf6d147521626203dedcfd901ceb3:4&background=039dfc");

        // Builder with base64 wearable
        // var base64Data =
        //     "eyJpZCI6IjUyNThhMmQxLTdiYTUtNGIwMi04MzY1LTg5MTZmMmUyMDgzZiIsIm5hbWUiOiJKYWNrZXQiLCJ0aHVtYm5haWwiOiJ0aHVtYm5haWwucG5nIiwiaW1hZ2UiOiJ0aHVtYm5haWwucG5nIiwiZGVzY3JpcHRpb24iOiIiLCJpMThuIjpbeyJjb2RlIjoiZW4iLCJ0ZXh0IjoiSmFja2V0In1dLCJkYXRhIjp7ImNhdGVnb3J5IjoidXBwZXJfYm9keSIsInJlcGxhY2VzIjpbXSwiaGlkZXMiOltdLCJyZW1vdmVzRGVmYXVsdEhpZGluZyI6WyJoYW5kcyJdLCJ0YWdzIjpbXSwicmVwcmVzZW50YXRpb25zIjpbeyJib2R5U2hhcGVzIjpbInVybjpkZWNlbnRyYWxhbmQ6b2ZmLWNoYWluOmJhc2UtYXZhdGFyczpCYXNlTWFsZSJdLCJtYWluRmlsZSI6Im1hbGUvamFja2V0LmdsYiIsImNvbnRlbnRzIjpbeyJrZXkiOiJtYWxlL2phY2tldC5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC5vcmcvdjEvc3RvcmFnZS9jb250ZW50cy9iYWZ5YmVpaHJleXNjYml3NXJkNXJ3anM1cHN6bGw1ZHFlb20zdWFxNXk3YXJ2YWF6a2hzZW11NHdkaSJ9XSwib3ZlcnJpZGVIaWRlcyI6W10sIm92ZXJyaWRlUmVwbGFjZXMiOltdfSx7ImJvZHlTaGFwZXMiOlsidXJuOmRlY2VudHJhbGFuZDpvZmYtY2hhaW46YmFzZS1hdmF0YXJzOkJhc2VGZW1hbGUiXSwibWFpbkZpbGUiOiJmZW1hbGUvamFja2V0LmdsYiIsImNvbnRlbnRzIjpbeyJrZXkiOiJmZW1hbGUvamFja2V0LmdsYiIsInVybCI6Imh0dHBzOi8vYnVpbGRlci1hcGkuZGVjZW50cmFsYW5kLm9yZy92MS9zdG9yYWdlL2NvbnRlbnRzL2JhZnliZWlocmV5c2NiaXc1cmQ1cndqczVwc3psbDVkcWVvbTN1YXE1eTdhcnZhYXpraHNlbXU0d2RpIn1dLCJvdmVycmlkZUhpZGVzIjpbXSwib3ZlcnJpZGVSZXBsYWNlcyI6W119XSwicmVxdWlyZWRQZXJtaXNzaW9ucyI6W10sImJsb2NrVnJtRXhwb3J0IjpmYWxzZSwiaXNTbWFydCI6ZmFsc2V9fQ";
        // ParseFromURL($"https://example.com/?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=00ffff&hairColor=00ffff&skinColor=aafbcc&upperBody=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&lowerBody=urn:decentraland:off-chain:base-avatars:kilt&background=039dfc&base64={base64Data}");

        // Builder with base64 emote
        // var base64Data =
        //     "eyJpZCI6ImZjMTZhMjlmLTAxZjQtNDI5MC1iZTY5LThjNGQ1ZDFlZDZlZSIsIm5hbWUiOiJDaGVmZiBraXNzIiwidGh1bWJuYWlsIjoidGh1bWJuYWlsLnBuZyIsImltYWdlIjoidGh1bWJuYWlsLnBuZyIsImRlc2NyaXB0aW9uIjoiIiwiaTE4biI6W3siY29kZSI6ImVuIiwidGV4dCI6IkNoZWZmIGtpc3MifV0sImVtb3RlRGF0YUFEUjc0Ijp7ImNhdGVnb3J5Ijoic3R1bnQiLCJsb29wIjpmYWxzZSwidGFncyI6W10sInJlcHJlc2VudGF0aW9ucyI6W3siYm9keVNoYXBlcyI6WyJ1cm46ZGVjZW50cmFsYW5kOm9mZi1jaGFpbjpiYXNlLWF2YXRhcnM6QmFzZU1hbGUiXSwibWFpbkZpbGUiOiJtYWxlL2NoZWZmIGtpc3MuZ2xiIiwiY29udGVudHMiOlt7ImtleSI6Im1hbGUvY2hlZmYga2lzcy5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC56b25lL3YxL3N0b3JhZ2UvY29udGVudHMvYmFma3JlaWV0enN2anZrcG9uNWV5d25uYmRtdGdlaHo2czVtYWNxeGd1eDVidWh6aGFhZnNiM3F0eW0ifV19LHsiYm9keVNoYXBlcyI6WyJ1cm46ZGVjZW50cmFsYW5kOm9mZi1jaGFpbjpiYXNlLWF2YXRhcnM6QmFzZUZlbWFsZSJdLCJtYWluRmlsZSI6ImZlbWFsZS9jaGVmZiBraXNzLmdsYiIsImNvbnRlbnRzIjpbeyJrZXkiOiJmZW1hbGUvY2hlZmYga2lzcy5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC56b25lL3YxL3N0b3JhZ2UvY29udGVudHMvYmFma3JlaWV0enN2anZrcG9uNWV5d25uYmRtdGdlaHo2czVtYWNxeGd1eDVidWh6aGFhZnNiM3F0eW0ifV19XX19";
        // ParseFromURL($"https://example.com/?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=00ffff&hairColor=00ffff&skinColor=aafbcc&upperBody=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&lowerBody=urn:decentraland:off-chain:base-avatars:kilt&background=039dfc&base64={base64Data}");

#endif

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

    private async Awaitable Reload()
    {
        if (_loading)
        {
            _shouldReload = true;
            return;
        }

        _loading = true;
        uiPresenter.EnableLoader(true);

        mainCamera.backgroundColor = Config.Background;

        await previewLoader.LoadPreview(Config);

        previewRotator.AllowVertical = Config.Mode is PreviewMode.Marketplace or PreviewMode.Builder;
        previewRotator.EnableAutoRotate = Config.Mode is PreviewMode.Marketplace && !previewLoader.HasEmoteOverride;
        previewRotator.ResetRotation();

        uiPresenter.EnableEmoteControls(previewLoader.HasEmoteOverride);
        uiPresenter.EnableZoom(Config.Mode is PreviewMode.Marketplace or PreviewMode.Builder);
        uiPresenter.EnableSwitcher(previewLoader.HasWearableOverride);
        uiPresenter.EnableAudioControls(previewLoader.HasEmoteAudio);

        uiPresenter.EnableLoader(false);
        _loading = false;

        if (_shouldReload)
        {
            _shouldReload = false;
            await Reload();
        }
    }
}