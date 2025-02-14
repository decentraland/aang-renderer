using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

public class UIPresenter : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private AvatarRoot avatarRoot;
    [SerializeField] private Material baseMat;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private WearablePreviewRotator wearablePreviewRotator;

    private void Start()
    {
        // UI
        var root = uiDocument.rootVisualElement;
        var graphicsLabel = root.Q<Label>("GraphicsAPI");
        graphicsLabel.text = $"API: {SystemInfo.graphicsDeviceType.ToString()}";
        root.Q<Button>("LoadButton").clicked += async () => await LoadAvatar(); // TODO: Nasty async void

        // TODO: Temporary fix for copy / paste in WebGL
        uiDocument.rootVisualElement.Query<TextField>().ForEach(v =>
        {
            v.AddManipulator(new WebGLSupport.WebGLInputManipulator());
        });

        // Common assets TODO: Improve maybe
        CommonAssets.AvatarMaterial = baseMat;
        CommonAssets.AvatarRoot = avatarRoot;
    }

    private async Awaitable LoadAvatar()
    {
        // Clear previous avatar
        avatarRoot.Clear();

        var userID = uiDocument.rootVisualElement.Q<TextField>("PlayerID").value;
        var wearableID = uiDocument.rootVisualElement.Q<TextField>("WearableID").value;
        if (string.IsNullOrEmpty(wearableID))
        {
            wearableID = uiDocument.rootVisualElement.Q<DropdownField>("WearableDropdown").value;
            if (wearableID == "None") wearableID = null;
        }

        await AvatarLoader.LoadAvatar(userID, wearableID);

        wearablePreviewRotator.Restart();

        // Animation
        var animators = avatarRoot.GetComponentsInChildren<Animator>();
        foreach (var animator in animators)
        {
            animator.runtimeAnimatorController = animatorController;
        }
    }

    [UsedImplicitly]
    private async void LoadAvatarExternal(string userID)
    {
        // Clear previous avatar
        avatarRoot.Clear();
        
        await AvatarLoader.LoadAvatar(userID, null);

        wearablePreviewRotator.Restart();

        // Animation
        var animators = avatarRoot.GetComponentsInChildren<Animator>();
        foreach (var animator in animators)
        {
            animator.runtimeAnimatorController = animatorController;
        }
    }
}