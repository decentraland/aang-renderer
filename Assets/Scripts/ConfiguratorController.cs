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
    [SerializeField] private PreviewRotator previewRotator;

    [SerializeField] private List<string> presetAvatars;
    [SerializeField] private string[] wearableCollection;

    private BodyShape _bodyShape;
    private readonly Dictionary<string, EntityDefinition> _selectedItems = new();
    private Color _skinColor;
    private Color _hairColor;
    private Color _eyeColor;
    private EntityDefinition _emoteToLoad;

    private ProfileResponse.Avatar.AvatarData[] _allPresets;

    public bool UseBrowserPreload { get; set; }

    private void Start()
    {
        uiPresenter.BodyShapeSelected += OnBodyShapeSelected;
        uiPresenter.WearableSelected += OnWearableSelected;
        uiPresenter.PresetSelected += OnPresetSelected;
        uiPresenter.SkinColorSelected += OnSkinColorSelected;
        uiPresenter.CharacterAreaDrag += previewRotator.OnDrag;

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

            _emoteToLoad = GetEmote(category);
        }

        uiPresenter.ClearPresetSelection();

        StartCoroutine(ReloadPreview());
    }

    private static EntityDefinition GetEmote(string category)
    {
        return EntityDefinition.FromEmbeddedEmote(category switch
        {
            "facial_hair" or "earring" or "hair" or "eyes" or "eyebrows" or "mouth" or "eyewear" =>
                $"character/Accessories_v0{Random.Range(1, 4)}",
            "upper_body" => $"character/Outfit_Upper_v0{Random.Range(1, 4)}",
            "lower_body" => $"character/Outfit_Lower_v0{Random.Range(1, 4)}",
            "feet" => $"character/Outfit_Shoes_v0{Random.Range(1, 3)}",
            "hands_wear" => $"character/Outfit_Hand_v0{Random.Range(1, 3)}",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        });
    }

    private static EntityDefinition GetEmote(BodyShape bodyShape)
    {
        return EntityDefinition.FromEmbeddedEmote(bodyShape switch
        {
            BodyShape.Male => "character/Wave_Male",
            BodyShape.Female => "character/Wave_Female",
            _ => throw new ArgumentOutOfRangeException(nameof(bodyShape), bodyShape, null)
        });
    }

    private void OnBodyShapeSelected(BodyShape bodyShape)
    {
        _bodyShape = bodyShape;
        uiPresenter.ClearPresetSelection();

        _emoteToLoad = GetEmote(bodyShape);

        StartCoroutine(ReloadPreview());
    }

    private void OnPresetSelected(ProfileResponse.Avatar.AvatarData avatar)
    {
        _selectedItems.Clear();

        // TODO: Fix
        _bodyShape = avatar.bodyShape == WearablesConstants.BODY_SHAPE_MALE ? BodyShape.Male : BodyShape.Female;

        _emoteToLoad = GetEmote(_bodyShape);

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
        await avatarLoader.LoadAvatar(_bodyShape, _selectedItems.Values, _emoteToLoad, Array.Empty<string>(),
            new AvatarColors(_eyeColor, _hairColor, _skinColor));
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
                    Application.streamingAssetsPath + "/character/Outfit_Hand_v01.glb",
                    Application.streamingAssetsPath + "/character/Outfit_Hand_v02.glb",
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