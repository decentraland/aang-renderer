using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UIPresenter : MonoBehaviour
{
    private const string USS_SWITCHER_BUTTON_SELECTED = "switcher__button--selected";
    private const float LOADER_SPEED = 360f;
    
    [SerializeField] private PreviewLoader previewLoader;
    
    private VisualElement _switcher;
    private VisualElement _wearableButton;
    private VisualElement _avatarButton;

    private VisualElement _loader;
    private VisualElement _loaderIcon;

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _switcher = root.Q("Switcher");
        _wearableButton = _switcher.Q("WearableButton");
        _avatarButton = _switcher.Q("AvatarButton");

        _loader = root.Q("Loader");
        _loaderIcon = _loader.Q("Icon");

        _wearableButton.AddManipulator(new Clickable(OnWearableButtonClicked));
        _avatarButton.AddManipulator(new Clickable(OnAvatarButtonClicked));
        
        // TODO: Debug panel

        // TODO: Temporary fix for copy / paste in Web builds
        root.Query<TextField>().ForEach(v =>
        {
            v.AddManipulator(new WebGLSupport.WebGLInputManipulator());
        });
    }

    private void Update()
    {
        // Rotate the loader icon
        _loaderIcon.RotateBy(LOADER_SPEED * Time.deltaTime);
    }

    public void EnableLoader(bool enable)
    {
        _loader.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
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
}

public static class UIExtensions
{
    public static void RotateBy(this VisualElement element, float angle)
    {
        element.style.rotate = new StyleRotate(new Rotate(new Angle(element.style.rotate.value.angle.value + angle)));
    }
}