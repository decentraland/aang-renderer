using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UI;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

[DefaultExecutionOrder(10)]
public class ConfiguratorController : MonoBehaviour
{
    [SerializeField] private ConfiguratorUIPresenter uiPresenter;
    [SerializeField] private AvatarLoader avatarLoader;
    [SerializeField] private PreviewRotator previewRotator;
    [SerializeField] private GameObject platform;

    [SerializeField] private List<CategoryDefinition> faceCategories;
    [SerializeField] private List<CategoryDefinition> bodyCategories;

    [FormerlySerializedAs("presets")] [Header("Presets")] [SerializeField]
    private PresetDefinition[] avatarPresets;

    [SerializeField] private Color[] skinColorPresets;
    [SerializeField] private Color[] hairColorPresets;

    private BodyShape _bodyShape;
    private readonly Dictionary<string, EntityDefinition> _selectedItems = new();
    private Color _skinColor;
    private Color _hairColor;
    private Color _eyeColor;
    private EntityDefinition _emoteToLoad;

    public bool UseBrowserPreload { get; set; }
    public string Username { get; set; }

    private void Start()
    {
        uiPresenter.BodyShapeSelected += OnBodyShapeSelected;
        uiPresenter.WearableSelected += OnWearableSelected;
        uiPresenter.PresetSelected += OnPresetSelected;
        uiPresenter.SkinColorSelected += OnSkinColorSelected;
        uiPresenter.CharacterAreaDrag += previewRotator.OnDrag;
        uiPresenter.Confirmed += OnConfirmed;

        StartCoroutine(InitialLoad());
    }

    private void OnConfirmed()
    {
        Debug.Log("Confirmed!");
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

    private void OnPresetSelected(PresetDefinition preset)
    {
        SetPreset(preset);

        StartCoroutine(ReloadPreview());
    }

    private void SetPreset(PresetDefinition preset)
    {
        _selectedItems.Clear();

        _bodyShape = preset.bodyShape;
        _skinColor = preset.skinColor;
        _hairColor = preset.hairColor;
        _eyeColor = preset.eyeColor;

        _emoteToLoad = GetEmote(_bodyShape);

        foreach (var urn in preset.urns)
        {
            var wearable = EntityService.GetCachedEntity(urn);
            _selectedItems[wearable.Category] = wearable;
        }

        uiPresenter.SetBodyShape(_bodyShape);
        uiPresenter.SetSelectedItems(_selectedItems);
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

        var allUrns = faceCategories.Union(bodyCategories)
            .SelectMany(c => c.urns).Where(urn => !string.IsNullOrEmpty(urn))
            .Append(WearablesConstants.BODY_SHAPE_MALE.ToLowerInvariant())
            .Append(WearablesConstants.BODY_SHAPE_FEMALE.ToLowerInvariant())
            .ToArray();
        var collectionEntities = await EntityService.GetEntities(allUrns);

        var entityDict = collectionEntities
            .ToDictionary(ae => ae.URN, ae => ae);

        // Update category definitions
        foreach (var cd in faceCategories.Union(bodyCategories))
        {
            foreach (var urn in cd.urns)
            {
                cd.Definitions.Add(string.IsNullOrEmpty(urn) ? null : entityDict[urn]);
            }
        }

        var randomPresetIndex = Random.Range(0, avatarPresets.Length);

        // Preload thumbnails
        RemoteTextureService.Instance.Pause(true);
        foreach (var preset in avatarPresets.Select(p => p.thumbnail))
        {
            RemoteTextureService.Instance.PreloadTexture(preset);
        }

        foreach (var ed in collectionEntities)
        {
            RemoteTextureService.Instance.PreloadTexture(ed.Thumbnail);
        }

        // Set data
        uiPresenter.SetUsername(Username);
        uiPresenter.SetCollection(faceCategories, bodyCategories);
        uiPresenter.SetPresets(avatarPresets, randomPresetIndex, skinColorPresets, hairColorPresets);
        RemoteTextureService.Instance.Pause(false);

        // Load initial preset
        SetPreset(avatarPresets[randomPresetIndex]);

        await ReloadPreview();

        platform.SetActive(true);
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

    // private static async Task<ProfileResponse.Avatar.AvatarData> LoadAvatar(string avatarID)
    // {
    //     var avatar = await APIService.GetAvatar(avatarID);
    //
    //     // Fix wearables
    //     for (var i = 0; i < avatar.wearables.Length; i++)
    //     {
    //         avatar.wearables[i] = avatar.wearables[i].ToLowerInvariant();
    //     }
    //
    //     return avatar;
    // }

    // private static PresetDefinition LoadAvatarConfig(string presetString)
    // {
    //     var split = presetString.Split('\t', StringSplitOptions.RemoveEmptyEntries);
    //     var bodyType = split[1] == "BaseFemale" ? BodyShape.Female : BodyShape.Male;
    //     var bodyColor = ColorUtility.TryParseHtmlString("#" + split[2], out var bc)
    //         ? bc
    //         : throw new Exception("Invalid body color");
    //     var hairColor = ColorUtility.TryParseHtmlString("#" + split[3], out var hc)
    //         ? hc
    //         : throw new Exception("Invalid hair color");
    //     var eyeColor = ColorUtility.TryParseHtmlString("#" + split[4], out var ec)
    //         ? ec
    //         : throw new Exception("Invalid eye color");
    //
    //     var urns = split.Where(s => s.StartsWith("urn")).ToArray();
    //
    //     var avatarData = new ProfileResponse.Avatar.AvatarData
    //     {
    //         bodyShape = bodyType == BodyShape.Female
    //             ? WearablesConstants.BODY_SHAPE_FEMALE
    //             : WearablesConstants
    //                 .BODY_SHAPE_MALE,
    //         wearables = urns,
    //         skin = new ProfileResponse.Avatar.AvatarData.ColorData { color = bodyColor },
    //         hair = new ProfileResponse.Avatar.AvatarData.ColorData { color = hairColor },
    //         eyes = new ProfileResponse.Avatar.AvatarData.ColorData { color = eyeColor },
    //         snapshots = new ProfileResponse.Avatar.AvatarData.Snapshot
    //         {
    //             body =
    //                 "https://profile-images-bucket-43d0c58.s3.us-east-1.amazonaws.com/v1/entities/bafkreifdwtultwt43oqe6zmkbqcfzbr22kziywuxjd5gx7zqanak7lnwxq/body.png"
    //         }
    //     };
    //
    //     Debug.Log(JsonUtility.ToJson(avatarData));
    //
    //     return new PresetDefinition()
    //     {
    //         bodyShape = bodyType,
    //         skinColor = bodyColor,
    //         hairColor = hairColor,
    //         eyeColor = eyeColor,
    //         urns = urns,
    //         thumbnail = "https://profile-images-bucket-43d0c58.s3.us-east-1.amazonaws.com/v1/entities/bafkreifdwtultwt43oqe6zmkbqcfzbr22kziywuxjd5gx7zqanak7lnwxq/body.png"
    //     };
    // }
}

[Serializable]
public class CategoryDefinition
{
    public string id;
    public string title;

    [FormerlySerializedAs("defaultThumbnail")]
    public Texture2D emptyThumbnail;

    public string[] urns;

    public List<EntityDefinition> Definitions { get; private set; } = new(20);
}

[Serializable]
public class PresetDefinition
{
    public BodyShape bodyShape;
    public Color skinColor;
    public Color hairColor;
    public Color eyeColor;
    public string thumbnail;
    public string[] urns;
}