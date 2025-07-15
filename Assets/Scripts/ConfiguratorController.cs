using System.Collections.Generic;
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

    private string _bodyShape;
    private readonly Dictionary<string, ActiveEntity> _selectedItems = new();
    private Color _skinColor;
    private Color _hairColor;
    private Color _eyeColor;

    private Dictionary<string, ActiveEntity> _allWearables;
    private ProfileResponse.Avatar.AvatarData[] _allPresets;

    private bool _presetsLoaded;
    private bool _collectionLoaded;
    private bool _previewLoaded;

    private void Start()
    {
        uiPresenter.BodyShapeSelected += OnBodyShapeSelected;
        uiPresenter.WearableSelected += OnWearableSelected;
        uiPresenter.PresetSelected += OnPresetSelected;
        uiPresenter.SkinColorSelected += OnSkinColorSelected;
        
        // TODO: Temporary colors
        _skinColor = new Color(1f, 0.894f, 0.776f);
        _hairColor = new Color(0.549f, 0.125f, 0.078f);
        _eyeColor = new Color(0.125f, 0.702f, 0.965f);

        StartCoroutine(InitialLoad());
    }

    private void OnSkinColorSelected(Color color)
    {
        _skinColor = color;

        StartCoroutine(ReloadPreview());
    }

    private void OnWearableSelected(string category, ActiveEntity wearable)
    {
        if (wearable == null)
        {
            _selectedItems.Remove(category);
        }
        else
        {
            _selectedItems[category] = wearable;
        }
        
        uiPresenter.ClearPresetSelection();

        StartCoroutine(ReloadPreview());
    }

    private void OnBodyShapeSelected(string bodyShape)
    {
        _bodyShape = bodyShape;
        uiPresenter.ClearPresetSelection();

        StartCoroutine(ReloadPreview());
    }

    private void OnPresetSelected(ProfileResponse.Avatar.AvatarData avatar)
    {
        _selectedItems.Clear();

        _bodyShape = avatar.bodyShape;

        foreach (var urn in avatar.wearables)
        {
            var wearable = _allWearables[urn];
            _selectedItems[wearable.metadata.data.category] = wearable;
        }
        
        uiPresenter.SetBodyShape(_bodyShape);
        uiPresenter.SetSelectedItems(_selectedItems);

        StartCoroutine(ReloadPreview());
    }

    private async Awaitable ReloadPreview()
    {
        Debug.Log("Reloading preview...");
        // TODO: We should pass ActiveEntities directly
        await previewLoader.LoadConfigurator(_bodyShape, _selectedItems.Values.Select(ae => ae.pointers[0]).ToList(), _eyeColor, _hairColor, _skinColor);
    }

    private async Awaitable InitialLoad()
    {
        Debug.Log("Initial loading...");
        var avatarTasks = presetAvatars.Select(LoadAvatar);
        var allWearables = (await APIService.GetWearableCollection(BASE_COLLECTION_ID)).wearables;

        var activeEntities = allWearables
            .Select(w => w.ToActiveEntity()).ToList();

        _allWearables = activeEntities.ToDictionary(ae => ae.pointers[0], ae => ae);

        var allCategories = activeEntities
            .GroupBy(ae => ae.metadata.data.category)
            .ToDictionary(ae => ae.Key, ae => ae.ToList());

        Debug.Log("Waiting for avatars...");
        _allPresets = await Task.WhenAll(avatarTasks);

        var randomPresetIndex = Random.Range(0, presetAvatars.Count);

        // Preload thumbnails
        foreach (var preset in _allPresets)
        {
            RemoteTextureService.Instance.PreloadTexture(preset.snapshots.body);
        }
        foreach (var (_, ae) in _allWearables)
        {
            RemoteTextureService.Instance.PreloadTexture(ae.metadata.thumbnail);
        }

        // Set data
        uiPresenter.SetCollection(allCategories);
        uiPresenter.SetPresets(_allPresets, randomPresetIndex);

        // Load initial preset
        OnPresetSelected(_allPresets[randomPresetIndex]);

        uiPresenter.LoadCompleted();
    }

    private static async Task<ProfileResponse.Avatar.AvatarData> LoadAvatar(string avatarID)
    {
        var avatar = await APIService.GetAvatar(avatarID);
        return avatar;
    }
}