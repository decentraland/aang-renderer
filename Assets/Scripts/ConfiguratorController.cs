using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Data;
using UI;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ConfiguratorController : MonoBehaviour
{
    private const string BASE_COLLECTION_ID = "urn:decentraland:off-chain:base-avatars";

    [SerializeField] private ConfiguratorUIPresenter uiPresenter;
    [SerializeField] private PreviewLoader previewLoader;
    [SerializeField] private List<string> presetAvatars;

    private bool _presetsLoaded;
    private bool _collectionLoaded;
    private bool _previewLoaded;

    private void Start()
    {
        uiPresenter.BodyShapeChanged += OnSetupChanged;
        uiPresenter.SetupChanged += OnSetupChanged;

        StartCoroutine(LoadPresets());
        StartCoroutine(LoadCollection());
        StartCoroutine(ReloadPreview());
    }

    private void OnSetupChanged()
    {
        StartCoroutine(ReloadPreview());
    }

    private async Awaitable ReloadPreview()
    {
        Debug.Log("Reloading preview...");
        await previewLoader.LoadConfigurator(uiPresenter.BodyShape,
            uiPresenter.Setup.Values.Where(ae => ae != null).Select(ae => ae.metadata.id).ToList());

        // For the initial load
        if (!_previewLoaded)
        {
            _previewLoaded = true;
            CheckLoadedStatus();
        }
    }

    private async Awaitable LoadPresets()
    {
        Debug.Log("Loading presets...");

        var avatars = await Task.WhenAll(presetAvatars.Select(LoadAvatar));
        uiPresenter.SetPresets(avatars);
        _presetsLoaded = true;
        CheckLoadedStatus();
        return;

        async Task<ProfileResponse.Avatar.AvatarData> LoadAvatar(string avatarID)
        {
            var avatar = await APIService.GetAvatar(avatarID);
            return avatar;
        }
    }

    private async Awaitable LoadCollection()
    {
        Debug.Log("Loading collection...");
        var allWearables = (await APIService.GetWearableCollection(BASE_COLLECTION_ID)).wearables;

        var categories = allWearables
            .Select(w => w.ToActiveEntity())
            .GroupBy(ae => ae.metadata.data.category)
            .ToDictionary(ae => ae.Key, ae => ae.ToList());

        Debug.Log($"Total wearables: {allWearables.Length}");
        foreach (var (category, items) in categories)
        {
            Debug.Log($"Category: {category} - {items.Count}");
        }

        uiPresenter.SetCollection(categories);
        _collectionLoaded = true;
        CheckLoadedStatus();
    }

    private void CheckLoadedStatus()
    {
        if (_presetsLoaded && _collectionLoaded && _previewLoaded)
        {
            uiPresenter.LoadCompleted();
        }
    }
}