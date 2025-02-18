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

    private string _currentInput = "";

    private void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _switcher = root.Q("Switcher");
        _wearableButton = _switcher.Q("WearableButton");
        _avatarButton = _switcher.Q("AvatarButton");

        _loader = root.Q("Loader");
        _loaderIcon = _loader.Q("Icon");

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
            _currentInput += c;

            if (!DEBUG_PASSPHRASE.StartsWith(_currentInput))
            {
                _currentInput = string.Empty;
                return;
            }

            if (_currentInput.Equals(DEBUG_PASSPHRASE))
            {
                EnableDebug();
                _currentInput = "";
            }
        }
    }

    private void EnableDebug()
    {
        var debugPanel = GetComponent<UIDocument>().rootVisualElement.Q("DebugPanel");
        debugPanel.style.display = DisplayStyle.Flex;

        debugPanel.Q<Button>("LoadButton").clicked += async () =>
        {
            var profileID = debugPanel.Q<TextField>("PlayerID").value;
            var wearableID = debugPanel.Q<TextField>("WearableID").value;
            if (string.IsNullOrEmpty(wearableID))
            {
                wearableID = debugPanel.Q<DropdownField>("WearableDropdown").value;
                if (wearableID == "None") wearableID = null;
            }

            await previewLoader.LoadPreview(profileID, wearableID);
        };
    }
}