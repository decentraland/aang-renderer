using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using UI;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

[DefaultExecutionOrder(10)]
public class ConfiguratorController : MonoBehaviour
{
    [SerializeField] private ConfiguratorUIPresenter uiPresenter;
    [SerializeField] private AvatarLoader avatarLoader;

    [SerializeField] private List<string> presetAvatars;
    [SerializeField] private string[] wearableCollection;

    private BodyShape _bodyShape;
    private readonly Dictionary<string, EntityDefinition> _selectedItems = new();
    private Color _skinColor;
    private Color _hairColor;
    private Color _eyeColor;

    private ProfileResponse.Avatar.AvatarData[] _allPresets;

    public bool UseBrowserPreload { get; set; }

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

    private void OnWearableSelected(string category, EntityDefinition wearable)
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

    private void OnBodyShapeSelected(BodyShape bodyShape)
    {
        _bodyShape = bodyShape;
        uiPresenter.ClearPresetSelection();

        StartCoroutine(ReloadPreview());
    }

    private void OnPresetSelected(ProfileResponse.Avatar.AvatarData avatar)
    {
        _selectedItems.Clear();

        // TODO: Fix
        _bodyShape = avatar.bodyShape == WearablesConstants.BODY_SHAPE_MALE ? BodyShape.Male : BodyShape.Female;

        foreach (var urn in avatar.wearables)
        {
            var wearable = EntityService.GetCachedEntity(urn);
            _selectedItems[wearable.Category] = wearable;
        }

        uiPresenter.SetBodyShape(_bodyShape);
        uiPresenter.SetSelectedItems(_selectedItems);

        StartCoroutine(ReloadPreview());
    }

    private async Awaitable ReloadPreview()
    {
        Debug.Log("Reloading preview...");
        // TODO: We should pass ActiveEntities directly
        
        await avatarLoader.LoadAvatar(_bodyShape, _selectedItems.Values, EntityDefinition.FromEmbeddedEmote("idle"), Array.Empty<string>(), new AvatarColors(_eyeColor, _hairColor, _skinColor));
    }

    private async Awaitable InitialLoad()
    {
        Debug.Log("Initial loading...");
        var avatarTasks = presetAvatars.Select(LoadAvatar);
        var collectionEntities = await EntityService.GetEntities(wearableCollection);

        var allCategories = collectionEntities
            .GroupBy(ed => ed.Category)
            .ToDictionary(ae => ae.Key, ae => ae.ToList());

        Debug.Log("Waiting for avatars...");
        _allPresets = await Task.WhenAll(avatarTasks);

        var randomPresetIndex = Random.Range(0, presetAvatars.Count);

        // Preload thumbnails
        foreach (var preset in _allPresets)
        {
            RemoteTextureService.Instance.PreloadTexture(preset.snapshots.body);
        }

        foreach (var ed in collectionEntities)
        {
            RemoteTextureService.Instance.PreloadTexture(ed.Thumbnail);
        }

        // Set data
        uiPresenter.SetCollection(allCategories);
        uiPresenter.SetPresets(_allPresets, randomPresetIndex);

        // Load initial preset
        OnPresetSelected(_allPresets[randomPresetIndex]);

        uiPresenter.LoadCompleted();

        if (UseBrowserPreload)
        {
            EntityService.PreloadCachedEntityAssets();
            JSBridge.NativeCalls.PreloadURLs(
                string.Join(',', Application.streamingAssetsPath + "/character/Accessories_v01.glb",
                    Application.streamingAssetsPath + "/character/Accessories_v02.glb",
                    Application.streamingAssetsPath + "/character/Accessories_v03.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Lower_v01.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Lower_v02.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Lower_v03.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Shoes_v01.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Shoes_v02.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Upper_v01.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Upper_v02.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Upper_v03.glb",
                    Application.streamingAssetsPath + "/character/Wave_Female.glb",
                    Application.streamingAssetsPath + "/character/Wave_Male.glb"));
        }
    }

    private static async Task<ProfileResponse.Avatar.AvatarData> LoadAvatar(string avatarID)
    {
        var avatar = await APIService.GetAvatar(avatarID);

        // Fix wearables
        for (var i = 0; i < avatar.wearables.Length; i++)
        {
            avatar.wearables[i] = avatar.wearables[i].ToLowerInvariant();
        }

        return avatar;
    }
}