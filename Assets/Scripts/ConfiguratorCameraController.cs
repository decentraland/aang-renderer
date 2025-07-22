using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

public class ConfiguratorCameraController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PreviewRotator previewRotator;
    [SerializeField] private ConfiguratorUIPresenter uiPresenter;
    [SerializeField] private CinemachineCamera fullBodyCamera;
    [SerializeField] private CinemachineCamera headCamera;
    [SerializeField] private CinemachineCamera upperBodyCamera;
    [SerializeField] private CinemachineCamera lowerBodyCamera;
    [SerializeField] private CinemachinePositionComposer[] positionComposers;

    private bool _hasZoomedOut;

    private void Start()
    {
        previewRotator.AllowVertical = false;
        previewRotator.EnableAutoRotate = false;
        previewRotator.LookAtCamera(false);

        uiPresenter.CategoryChanged += OnCategoryChanged;
        uiPresenter.CharacterAreaCenterChanged += OnCharacterAreaCenterChanged;
        uiPresenter.CharacterAreaZoom += OnCharacterAreaZoom;

        // Set full body camera
        OnCategoryChanged(null);
    }

    private void OnCharacterAreaZoom(float delta)
    {
        switch (delta)
        {
            case < 0 when !fullBodyCamera.gameObject.activeSelf:
                _hasZoomedOut = true;
                fullBodyCamera.gameObject.SetActive(true);
                break;
            case > 0 when _hasZoomedOut:
                _hasZoomedOut = false;
                fullBodyCamera.gameObject.SetActive(false);
                break;
        }
    }

    private void OnCharacterAreaCenterChanged(Vector2 screenSpaceCenter)
    {
        foreach (var composer in positionComposers)
        {
            composer.Composition.ScreenPosition = screenSpaceCenter;
        }
    }

    private void OnCategoryChanged(string category)
    {
        _hasZoomedOut = false;

        var useFullBodyCamera = false;
        var useHeadCamera = false;
        var useUpperBodyCamera = false;
        var useLowerBodyCamera = false;

        switch (category)
        {
            case "mouth":
            case "eyewear":
            case "facial_hair":
            case "earring":
            case "hair":
            case "eyes":
            case "eyebrows":
                useHeadCamera = true;
                break;
            case "lower_body":
            case "feet":
                useLowerBodyCamera = true;
                break;
            case "hands_wear":
            case "upper_body":
                useUpperBodyCamera = true;
                break;
            default:
                useFullBodyCamera = true;
                break;
        }

        fullBodyCamera.gameObject.SetActive(useFullBodyCamera);
        headCamera.gameObject.SetActive(useHeadCamera);
        upperBodyCamera.gameObject.SetActive(useUpperBodyCamera);
        lowerBodyCamera.gameObject.SetActive(useLowerBodyCamera);
        
        previewRotator.LookAtCamera(true);
    }
}