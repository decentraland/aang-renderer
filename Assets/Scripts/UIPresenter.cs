using System.Collections.Generic;
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

    private VisualElement _switcher;
    private VisualElement _wearableButton;
    private VisualElement _avatarButton;

    private VisualElement _loader;
    private VisualElement _loaderIcon;

    // Builder
    private DropdownField _bodyShapeDropdown;
    private EnumField _bodyShapeEnum;
    private DropdownField _eyeColorDropdown;
    private DropdownField _hairDropdown;
    private DropdownField _upperBodyDropdown;
    private DropdownField _skinColorDropdown;
    private DropdownField _hairColorDropdown;
    private DropdownField _facialHairDropdown;
    private DropdownField _lowerBodyDropdown;

    private string _currentDebugInput = "";
    private bool _debugLoaded;

    private void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _switcher = root.Q("Switcher");
        _wearableButton = _switcher.Q("WearableButton");
        _avatarButton = _switcher.Q("AvatarButton");

        _loader = root.Q("Loader");
        _loaderIcon = _loader.Q("Icon");

        _bodyShapeDropdown = root.Q<DropdownField>("BodyShape");
        _eyeColorDropdown = root.Q<DropdownField>("EyeColor");
        _hairDropdown = root.Q<DropdownField>("Hair");
        _upperBodyDropdown = root.Q<DropdownField>("UpperBody");
        _skinColorDropdown = root.Q<DropdownField>("SkinColor");
        _hairColorDropdown = root.Q<DropdownField>("HairColor");
        _facialHairDropdown = root.Q<DropdownField>("FacialHair");
        _lowerBodyDropdown = root.Q<DropdownField>("LowerBody");

        _bodyShapeEnum = root.Q<EnumField>("BodyShape");

        _wearableButton.AddManipulator(new Clickable(OnWearableButtonClicked));
        _avatarButton.AddManipulator(new Clickable(OnAvatarButtonClicked));

        // TODO: Temporary fix for copy / paste in Web builds
        root.Query<TextField>().ForEach(v => v.AddManipulator(new WebGLSupport.WebGLInputManipulator()));
    }

    private void Update()
    {
        // Rotate the loader icon
        _loaderIcon.RotateBy(LOADER_SPEED * Time.deltaTime);

        CheckDebug();
    }

    public void EnableSwitcher(bool enable)
    {
        // We use visibility instead of display so that EnableLoader won't override it
        _switcher.style.visibility = enable ? Visibility.Visible : Visibility.Hidden;
    }

    public void EnableLoader(bool enable)
    {
        _loader.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        _switcher.style.display = enable ? DisplayStyle.None : DisplayStyle.Flex;
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