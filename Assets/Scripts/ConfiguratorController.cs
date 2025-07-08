using System.Diagnostics;
using System.Linq;
using Data;
using UI;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ConfiguratorController : MonoBehaviour
{
    private const string BASE_COLLECTION_ID = "urn:decentraland:off-chain:base-avatars";

    [SerializeField] private ConfiguratorUIPresenter uiPresenter;
    [SerializeField] private PreviewLoader previewLoader;

    private void Start()
    {
        uiPresenter.SetupChanged += OnSetupChanged;
        
        StartCoroutine(LoadCollection());
        StartCoroutine(ReloadPreview());
    }

    private void OnSetupChanged()
    {
        StartCoroutine(ReloadPreview());
    }

    private async Awaitable ReloadPreview()
    {
        await previewLoader.LoadConfigurator(uiPresenter.BodyShape,
            uiPresenter.Setup.Values.Select(wd => wd.Pointer).ToList());
    }

    private async Awaitable LoadCollection()
    {
        uiPresenter.ShowLoading(true);
        
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

        uiPresenter.ShowLoading(false);
    }
}