using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

[RequireComponent(typeof(UIDocument))]
public class UIPresenter : MonoBehaviour
{
    private const string USS_SWITCHER_BUTTON_SELECTED = "switcher__button--selected";
    private const float LOADER_SPEED = 360f;
    private const string DEBUG_PASSPHRASE = "debugmesilly";

    [SerializeField] private PreviewLoader previewLoader;
    [SerializeField] private PreviewRotator _previewRotator;
    [SerializeField] private Bootstrap bootstrap;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private CameraController cameraController;

    private VisualElement _switcher;
    private VisualElement _wearableButton;
    private VisualElement _avatarButton;

    private VisualElement _zoomControls;
    private Button _zoomInButton;
    private Button _zoomOutButton;

    private VisualElement _emoteControls;
    private Button _playEmoteButton;
    private Label _playEmoteLabel;
    private Button _muteEmoteButton;

    private VisualElement _controls;
    private VisualElement _loader;
    private VisualElement _loaderIcon;

    private string _currentDebugInput = "";
    private bool _debugLoaded;
    private bool _animationPlaying = true;

    private void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _switcher = root.Q("Switcher");
        _wearableButton = _switcher.Q("WearableButton");
        _avatarButton = _switcher.Q("AvatarButton");

        _zoomControls = root.Q("ZoomControls");
        _zoomInButton = _zoomControls.Q<Button>("ZoomInButton");
        _zoomOutButton = _zoomControls.Q<Button>("ZoomOutButton");
        _zoomInButton.clicked += cameraController.ZoomIn;
        _zoomOutButton.clicked += cameraController.ZoomOut;

        _emoteControls = root.Q("EmoteControls");
        _playEmoteButton = _emoteControls.Q<Button>("PlayStopButton");
        _playEmoteLabel = _playEmoteButton.Q<Label>("Title");
        _muteEmoteButton = _emoteControls.Q<Button>("MuteButton");
        _playEmoteButton.clicked += OnPlayPauseEmoteClicked;
        _muteEmoteButton.clicked += OnMuteEmoteClicked;

        _controls = root.Q("Controls");
        _loader = root.Q("Loader");
        _loaderIcon = _loader.Q("Icon");

        _wearableButton.AddManipulator(new Clickable(OnWearableButtonClicked));
        _avatarButton.AddManipulator(new Clickable(OnAvatarButtonClicked));

        if (Application.isEditor)
        {
            EnableDebug();
        }

        ShowLoader(true);
    }

    private void Update()
    {
        // Rotate the loader icon
        _loaderIcon.RotateBy(LOADER_SPEED * Time.deltaTime);

        CheckDebug();
    }

    public void EnableSwitcher(bool enable)
    {
        _switcher.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void EnableZoom(bool enable)
    {
        _zoomControls.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void EnableEmoteControls(bool enable)
    {
        _emoteControls.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void EnableAudioControls(bool enable)
    {
        _muteEmoteButton.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void EnableLoader(bool enable)
    {
        _loaderIcon.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void ShowLoader(bool enable)
    {
        _loader.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        _controls.style.display = enable ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void OnAvatarButtonClicked()
    {
        PlayerPrefs.SetInt("PreviewAvatarShown", 1);
        ShowAvatar(true);
    }

    private void OnWearableButtonClicked()
    {
        PlayerPrefs.SetInt("PreviewAvatarShown", 0);
        ShowAvatar(false);
    }

    public void ShowAvatar(bool show)
    {
        _wearableButton.EnableInClassList(USS_SWITCHER_BUTTON_SELECTED, !show);
        _avatarButton.EnableInClassList(USS_SWITCHER_BUTTON_SELECTED, show);

        previewLoader.ShowAvatar(show);
        _previewRotator.ResetRotation();
    }

    private void OnMuteEmoteClicked()
    {
        audioSource.mute = !audioSource.mute;
        _muteEmoteButton.EnableInClassList("emote-controls__button-mute--muted", audioSource.mute);
    }

    private void OnPlayPauseEmoteClicked()
    {
        _animationPlaying = !_animationPlaying;

        previewLoader.PlayAnimation(_animationPlaying);
        _playEmoteButton.EnableInClassList("emote-controls__button-play--stopped", !_animationPlaying);
        _playEmoteLabel.text = _animationPlaying ? "STOP EMOTE" : "PLAY EMOTE";

        if (!_animationPlaying)
        {
            audioSource.Stop();
        }
    }

    private void CheckDebug()
    {
        foreach (var c in Input.inputString)
        {
            _currentDebugInput += c;

            if (!DEBUG_PASSPHRASE.StartsWith(_currentDebugInput))
            {
                _currentDebugInput = string.Empty;
                return;
            }

            if (_currentDebugInput.Equals(DEBUG_PASSPHRASE))
            {
                EnableDebug();
                _currentDebugInput = "";
            }
        }
    }

    private void EnableDebug()
    {
        var debugPanel = GetComponent<UIDocument>().rootVisualElement.Q("DebugPanel");
        debugPanel.style.display = DisplayStyle.Flex;

        if (_debugLoaded) return;

        // @formatter:off
        var dropdownOptions = new List<(string name, string url)>
        {
            ("From URL", null),
            ("Profile", "https://example.com/?mode=profile&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc"),
            ("Authentication", "https://example.com/?mode=authentication&profile=0x3f574d05ec670fe2c92305480b175654ca512005&background=039dfc"),
            ("Market Wearable", "https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x1b4e20251ec5da51c749f96a4993f3cebf066853:0&background=039dfc"),
            ("Market Emote", "https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0xb5e24ada4096b86ce3cf7af5119f19ed6089a80b:0&background=039dfc"),
            ("Market Emote Prop", "https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0x97822560ec3e3522c1237f85817003211281eb79:0&background=039dfc"),
            ("Market Emote Audio", "https://example.com/?mode=marketplace&profile=0x3f574d05ec670fe2c92305480b175654ca512005&urn=urn:decentraland:matic:collections-v2:0xb187264af67cf6d147521626203dedcfd901ceb3:4&background=039dfc"),
            ("Builder", "https://example.com/?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=20B3F6&skinColor=FFE4C6&hairColor=8C2014&urn=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&urn=urn:decentraland:off-chain:base-avatars:kilt&background=4b4851"),
            ("Builder Base64 Emote", "https://example.com/?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=20B3F6&skinColor=FFE4C6&hairColor=8C2014&urn=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&urn=urn:decentraland:off-chain:base-avatars:kilt&background=4b4851&base64=eyJpZCI6ImZjMTZhMjlmLTAxZjQtNDI5MC1iZTY5LThjNGQ1ZDFlZDZlZSIsIm5hbWUiOiJDaGVmZiBraXNzIiwidGh1bWJuYWlsIjoidGh1bWJuYWlsLnBuZyIsImltYWdlIjoidGh1bWJuYWlsLnBuZyIsImRlc2NyaXB0aW9uIjoiIiwiaTE4biI6W3siY29kZSI6ImVuIiwidGV4dCI6IkNoZWZmIGtpc3MifV0sImVtb3RlRGF0YUFEUjc0Ijp7ImNhdGVnb3J5Ijoic3R1bnQiLCJsb29wIjpmYWxzZSwidGFncyI6W10sInJlcHJlc2VudGF0aW9ucyI6W3siYm9keVNoYXBlcyI6WyJ1cm46ZGVjZW50cmFsYW5kOm9mZi1jaGFpbjpiYXNlLWF2YXRhcnM6QmFzZU1hbGUiXSwibWFpbkZpbGUiOiJtYWxlL2NoZWZmIGtpc3MuZ2xiIiwiY29udGVudHMiOlt7ImtleSI6Im1hbGUvY2hlZmYga2lzcy5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC56b25lL3YxL3N0b3JhZ2UvY29udGVudHMvYmFma3JlaWV0enN2anZrcG9uNWV5d25uYmRtdGdlaHo2czVtYWNxeGd1eDVidWh6aGFhZnNiM3F0eW0ifV19LHsiYm9keVNoYXBlcyI6WyJ1cm46ZGVjZW50cmFsYW5kOm9mZi1jaGFpbjpiYXNlLWF2YXRhcnM6QmFzZUZlbWFsZSJdLCJtYWluRmlsZSI6ImZlbWFsZS9jaGVmZiBraXNzLmdsYiIsImNvbnRlbnRzIjpbeyJrZXkiOiJmZW1hbGUvY2hlZmYga2lzcy5nbGIiLCJ1cmwiOiJodHRwczovL2J1aWxkZXItYXBpLmRlY2VudHJhbGFuZC56b25lL3YxL3N0b3JhZ2UvY29udGVudHMvYmFma3JlaWV0enN2anZrcG9uNWV5d25uYmRtdGdlaHo2czVtYWNxeGd1eDVidWh6aGFhZnNiM3F0eW0ifV19XX19"),
            ("Builder Anim Ref", "https://example.com/?mode=builder&bodyShape=urn:decentraland:off-chain:base-avatars:BaseMale&eyeColor=20B3F6&skinColor=FFE4C6&hairColor=8C2014&urn=urn:decentraland:off-chain:base-avatars:turtle_neck_sweater&urn=urn:decentraland:off-chain:base-avatars:kilt&urn=urn:decentraland:off-chain:base-avatars:keanu_hair&background=4b4851&showAnimationReference=true"),
        };
        // @formatter:on

        var dropdown = debugPanel.Q<DropdownField>("URLDropdown");
        dropdown.choices = dropdownOptions.Select(o => o.name).ToList();
        dropdown.index = 0;
        dropdown.RegisterValueChangedCallback(evt =>
        {
            var selected = dropdownOptions.Find(o => o.name == evt.newValue);
            bootstrap.ParseFromURL(selected.url);
            bootstrap.InvokeReload();
        });

        var methodNameDropdown = debugPanel.Q<DropdownField>("MethodNameDropdown");
        methodNameDropdown.choices = typeof(JSBridge)
            .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name)
            .ToList();
        methodNameDropdown.index = 0;

        var parameterField = debugPanel.Q<TextField>("Parameter");
        debugPanel.Q<Button>("InvokeButton").clicked += () =>
        {
            if (string.IsNullOrEmpty(parameterField.value))
            {
                GameObject.Find("JSBridge").SendMessage(methodNameDropdown.value);
            }
            else
            {
                GameObject.Find("JSBridge").SendMessage(methodNameDropdown.value, parameterField.value);
            }
        };

        debugPanel.Q<Button>("HideButton").clicked += () => debugPanel.style.display = DisplayStyle.None;

        _debugLoaded = true;
    }
}