using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UI.Elements;
using UI.Views;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class ConfiguratorUIPresenter : MonoBehaviour
    {
        [SerializeField] private List<string> faceCategories;
        [SerializeField] private List<string> bodyCategories;

        private readonly Dictionary<string, string> categoryLocalizations = new()
        {
            { "eyewear", "EYEWEAR" },
            { "upper_body", "UPPER BODY" },
            { "facial_hair", "FACIAL HAIR" },
            { "lower_body", "LOWER BODY" },
            { "feet", "FEET" },
            { "hands_wear", "HANDS" },
            { "earring", "EARRINGS" },
            { "hair", "HAIR" },
            { "eyes", "EYES" },
            { "eyebrows", "EYEBROWS" },
            { "mouth", "MOUTH" },
        };

        private VisualElement _configuratorContainer;
        private VisualElement _loader;
        private VisualElement _loaderIcon;

        private DCLButtonElement _backButton;
        private DCLButtonElement _skipButton;
        private DCLButtonElement _confirmButton;

        private Label _stageTitle;

        public string BodyShape { get; private set; } = "urn:decentraland:off-chain:base-avatars:BaseMale"; // TODO: Fix
        public Dictionary<string, ActiveEntity> Setup { get; } = new();

        private Dictionary<string, List<ActiveEntity>> _collection;
        private List<(string category, List<ActiveEntity> entities)> _faceEntities;
        private List<(string category, List<ActiveEntity> entities)> _bodyEntities;

        // Views
        private WearablesView _headWearablesView;
        private WearablesView _bodyWearablesView;
        private PresetsView _presetsView;
        private BodyTypePopupView _bodyTypePopupView;

        private Stage _currentStage = Stage.Preset;

        public event Action BodyShapeChanged;
        public event Action SetupChanged;

        private void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _configuratorContainer = root.Q("Container");

            _stageTitle = root.Q<Label>("StageTitle");

            _backButton = root.Q<DCLButtonElement>("BackButton");
            _skipButton = root.Q<DCLButtonElement>("SkipButton");
            _confirmButton = root.Q<DCLButtonElement>("ConfirmButton");

            _backButton.Clicked += OnBackClicked;
            _confirmButton.Clicked += OnConfirmClicked;

            _loader = root.Q("Loader");
            _loaderIcon = _loader.Q("Icon");

            var presetsContainer = root.Q("Presets");
            _presetsView = new PresetsView(presetsContainer);

            // Dropdowns
            var bodyTypeDropdown = root.Q<DCLDropdownElement>("BodyTypeDropdown");
            _bodyTypePopupView = new BodyTypePopupView(bodyTypeDropdown.Q("BodyTypePopup"));
            _bodyTypePopupView.BodyTypeChanged += OnBodyTypeChanged;

            var headWearablesContainer = root.Q("HeadWearables");
            _headWearablesView = new WearablesView(
                headWearablesContainer,
                headWearablesContainer.Q<Label>("CategoryHeader"),
                headWearablesContainer.Q<VisualElement>("Sidebar"),
                headWearablesContainer.Q("Items"),
                categoryLocalizations
            );
            _headWearablesView.WearableSelected += OnWearableSelected;

            var bodyWearablesContainer = root.Q("BodyWearables");
            _bodyWearablesView = new WearablesView(
                bodyWearablesContainer,
                bodyWearablesContainer.Q<Label>("CategoryHeader"),
                bodyWearablesContainer.Q<VisualElement>("Sidebar"),
                bodyWearablesContainer.Q("Items"),
                categoryLocalizations
            );

            _configuratorContainer.SetDisplay(false);
            _loader.SetDisplay(true);
            ShowStage(_currentStage = Stage.Preset);
        }

        private void OnBodyTypeChanged(bool isMale)
        {
            BodyShape = isMale
                ? "urn:decentraland:off-chain:base-avatars:BaseMale"
                : "urn:decentraland:off-chain:base-avatars:BaseFemale";

            BodyShapeChanged?.Invoke();
        }

        private void OnBackClicked()
        {
            HideStage(_currentStage);
            _currentStage = (Stage)((int)_currentStage - 1);
            ShowStage(_currentStage);
        }

        private void OnConfirmClicked()
        {
            if (_currentStage == Stage.Body)
            {
                Debug.Log("DONE!");
                return;
            }

            HideStage(_currentStage);
            _currentStage = (Stage)((int)_currentStage + 1);
            ShowStage(_currentStage);
        }

        private void HideStage(Stage stage)
        {
            switch (stage)
            {
                case Stage.Preset:
                    _presetsView.Show(false);
                    break;
                case Stage.Face:
                    _headWearablesView.Show(false);
                    break;
                case Stage.Body:
                    _bodyWearablesView.Show(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        private void ShowStage(Stage stage)
        {
            switch (stage)
            {
                case Stage.Preset:
                    _presetsView.Show(true);
                    _stageTitle.text = "1. Choose your starting look";
                    _confirmButton.Text = "START CUSTOMIZING";
                    _skipButton.style.display = DisplayStyle.Flex;
                    _backButton.style.display = DisplayStyle.None;
                    break;
                case Stage.Face:
                    _headWearablesView.Show(true);
                    _stageTitle.text = "2. Customize your face";
                    _confirmButton.Text = "CONFIRM FACE";
                    _skipButton.style.display = DisplayStyle.Flex;
                    _backButton.style.display = DisplayStyle.Flex;
                    break;
                case Stage.Body:
                    _bodyWearablesView.Show(true);
                    _stageTitle.text = "2. Customize your outfit";
                    _confirmButton.Text = "FINISH";
                    _skipButton.style.display = DisplayStyle.None;
                    _backButton.style.display = DisplayStyle.Flex;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Update()
        {
            // Rotate the loader icon
            _loaderIcon.RotateBy(360f * Time.deltaTime);
        }

        public void LoadCompleted()
        {
            _configuratorContainer.SetDisplay(true);
            _loader.SetDisplay(false);
        }

        public void SetPresets(ProfileResponse.Avatar.AvatarData[] presets)
        {
            _presetsView.SetPresets(presets);
        }

        public void SetCollection(Dictionary<string, List<ActiveEntity>> collection)
        {
            /*
               Category: eyewear - 14
               Category: upper_body - 56
               Category: facial_hair - 13
               Category: body_shape - 2
               Category: lower_body - 38
               Category: feet - 24
               Category: hands_wear - 4
               Category: tiara - 5
               Category: earring - 12
               Category: hair - 33
               Category: eyebrows - 26
               Category: eyes - 35
               Category: mouth - 20
             */

            _faceEntities = faceCategories.Select(cat => (cat, collection[cat].Take(20).ToList())).ToList();
            _bodyEntities = bodyCategories.Select(cat => (cat, collection[cat].Take(20).ToList())).ToList();

            // Preload thumbnails
            foreach (var (_, entities) in _faceEntities.Union(_bodyEntities))
            {
                foreach (var ae in entities)
                {
                    RemoteTextureService.Instance.PreloadTexture(ae.metadata.thumbnail);
                }
            }

            _headWearablesView.SetCollection(_faceEntities);
            _bodyWearablesView.SetCollection(_bodyEntities);
        }

        private void OnWearableSelected(string category, ActiveEntity ae)
        {
            Setup[category] = ae;
            SetupChanged?.Invoke();
        }

        private enum Stage
        {
            Preset,
            Face,
            Body
        }
    }
}