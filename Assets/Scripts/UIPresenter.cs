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

        EnableLoader(true);
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
        _loader.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        _controls.style.display = enable ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void OnAvatarButtonClicked()
    {
        _wearableButton.RemoveFromClassList(USS_SWITCHER_BUTTON_SELECTED);
        _avatarButton.AddToClassList(USS_SWITCHER_BUTTON_SELECTED);

        previewLoader.ShowAvatar(true);
    }

    private void OnWearableButtonClicked()
    {
        _avatarButton.RemoveFromClassList(USS_SWITCHER_BUTTON_SELECTED);
        _wearableButton.AddToClassList(USS_SWITCHER_BUTTON_SELECTED);

        previewLoader.ShowAvatar(false);
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

        debugPanel.Q<Button>("LoadButton").clicked += async () =>
        {
            var profileID = debugPanel.Q<TextField>("PlayerID").value;
            var wearableID = debugPanel.Q<TextField>("WearableID").value;
            if (string.IsNullOrEmpty(wearableID))
            {
                wearableID = debugPanel.Q<DropdownField>("WearableDropdown").value;
                if (wearableID == "None") wearableID = null;
            }

            await previewLoader.LoadPreview(
                URLParser.Parse($"https://example.com/?mode=marketplace&profile={profileID}&urn={wearableID}"));
        };
        debugPanel.Q<Button>("HideButton").clicked += () => debugPanel.style.display = DisplayStyle.None;

        _debugLoaded = true;
    }
}