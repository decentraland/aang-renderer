using System;
using System.Collections.Generic;
using Data;
using UI.Elements;
using UI.Manipulators;
using UI.Views;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

namespace UI
{
    [DefaultExecutionOrder(10)]
    public class ConfiguratorUIPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        // TODO: Maybe move to controller?
        [SerializeField] private Color[] presetSkinColors;
        [SerializeField] private Color[] presetHairColors;

        private VisualElement _configuratorContainer;
        private VisualElement _loader;
        private VisualElement _loaderIcon;

        private DCLButtonElement _backButton;
        private DCLButtonElement _skipButton;
        private DCLButtonElement _confirmButton;

        private Label _stageTitle;

        private string _username;

        // Views
        private WearablesView _headWearablesView;
        private WearablesView _bodyWearablesView;
        private PresetsView _presetsView;
        private ConfirmPopupView _confirmPopupView;
        private BodyShapePopupView _bodyShapePopupView;
        private ColorPopupView _skinColorPopupView;
        private ColorPopupView _hairColorPopupView;

        private Stage _currentStage = Stage.Preset;

        public event Action<Vector2> CharacterAreaCenterChanged;
        public event Action<float> CharacterAreaZoom;
        public event Action<Vector2> CharacterAreaDrag;

        public event Action<string> CategoryChanged;
        public event Action<Color> SkinColorSelected;
        public event Action<Color> HairColorSelected;
        public event Action<BodyShape> BodyShapeSelected;
        public event Action<string, EntityDefinition> WearableSelected;
        public event Action<ProfileResponse.Avatar.AvatarData> PresetSelected;

        public event Action Confirmed;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;

            _configuratorContainer = root.Q("Container");
            var characterArea = root.Q("CharacterArea");

            characterArea.RegisterCallback<GeometryChangedEvent, VisualElement>((_, area) =>
            {
                var panel = area.panel;
                var layoutSize = panel.visualTree.layout; // This is in panel space

                var elementCenter = area.worldBound.center;
                var panelCenter = layoutSize.center;

                var offsetFromCenter = elementCenter - panelCenter;
                var normalized = new Vector2(
                    offsetFromCenter.x / layoutSize.width,
                    offsetFromCenter.y / layoutSize.height
                );

                CharacterAreaCenterChanged!(normalized);
            }, characterArea);
            characterArea.RegisterCallback<WheelEvent, ConfiguratorUIPresenter>(
                (evt, p) => p.CharacterAreaZoom!(evt.delta.y), this);
            characterArea.AddManipulator(new DragManipulator(d => CharacterAreaDrag!(d)));

            _stageTitle = root.Q<Label>("StageTitle");

            _backButton = root.Q<DCLButtonElement>("BackButton");
            _skipButton = root.Q<DCLButtonElement>("SkipButton");
            _confirmButton = root.Q<DCLButtonElement>("ConfirmButton");

            _backButton.Clicked += OnBackClicked;
            _confirmButton.Clicked += OnNextClicked;
            _skipButton.Clicked += () => _confirmPopupView.Show(true);

            _loader = root.Q("Loader");
            _loaderIcon = _loader.Q("Icon");

            var presetsContainer = root.Q("Presets");
            _presetsView = new PresetsView(presetsContainer);
            _presetsView.PresetSelected += preset => PresetSelected!(preset);

            // Dropdowns
            var bodyTypeDropdown = root.Q<DCLDropdownElement>("BodyTypeDropdown");
            _bodyShapePopupView = new BodyShapePopupView(bodyTypeDropdown.Q("BodyTypePopup"));
            _bodyShapePopupView.BodyShapeSelected += bs => BodyShapeSelected!(bs);

            var skinColorDropdown = root.Q<DCLDropdownElement>("SkinColorDropdown");
            _skinColorPopupView = new ColorPopupView(skinColorDropdown.Q("ColorPopup"), skinColorDropdown.Icon,
                presetSkinColors);
            _skinColorPopupView.ColorSelected += skinColor => SkinColorSelected!(skinColor);

            // var hairColorDropdown = root.Q<DCLDropdownElement>("HairColorDropdown");
            // _hairColorPopupView = new ColorPopupView(hairColorDropdown.Q("ColorPopup"), hairColorDropdown.Icon,
            //     presetHairColors);
            // _hairColorPopupView.ColorSelected += hairColor => HairColorSelected!(hairColor);

            var headWearablesContainer = root.Q("HeadWearables");
            _headWearablesView = new WearablesView(
                headWearablesContainer,
                headWearablesContainer.Q<Label>("CategoryHeader"),
                headWearablesContainer.Q<VisualElement>("Sidebar"),
                headWearablesContainer.Q("Items")
            );
            _headWearablesView.WearableSelected += (c, ae) => WearableSelected!(c, ae);
            _headWearablesView.CategoryChanged += c => CategoryChanged!(c);

            var bodyWearablesContainer = root.Q("BodyWearables");
            _bodyWearablesView = new WearablesView(
                bodyWearablesContainer,
                bodyWearablesContainer.Q<Label>("CategoryHeader"),
                bodyWearablesContainer.Q<VisualElement>("Sidebar"),
                bodyWearablesContainer.Q("Items")
            );
            _bodyWearablesView.WearableSelected += (c, ae) => WearableSelected!(c, ae);
            _bodyWearablesView.CategoryChanged += c => CategoryChanged!(c);

            _confirmPopupView = new ConfirmPopupView(root.Q("ConfirmationPopup"));
            _confirmPopupView.Confirmed += () => Confirmed!();

            _configuratorContainer.SetVisibility(false);
            _loader.SetDisplay(true);
        }

        private void OnBackClicked()
        {
            HideStage(_currentStage);
            _currentStage = (Stage)((int)_currentStage - 1);
            ShowStage(_currentStage);
        }

        private void OnNextClicked()
        {
            if (_currentStage == Stage.Body)
            {
                _confirmPopupView.Show(true);
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
                    _stageTitle.text = $"1. Choose {_username}'s starting look";
                    _confirmButton.Text = "START CUSTOMIZING";
                    _skipButton.style.display = DisplayStyle.Flex;
                    _backButton.style.display = DisplayStyle.None;
                    CategoryChanged!(null);
                    break;
                case Stage.Face:
                    _headWearablesView.Show(true);
                    _stageTitle.text = $"2. Customize {_username}'s face";
                    _confirmButton.Text = "CONFIRM FACE";
                    _skipButton.style.display = DisplayStyle.Flex;
                    _backButton.style.display = DisplayStyle.Flex;
                    CategoryChanged!(_headWearablesView.SelectedCategory);
                    break;
                case Stage.Body:
                    _bodyWearablesView.Show(true);
                    _stageTitle.text = $"2. Customize {_username}'s outfit";
                    _confirmButton.Text = "FINISH";
                    _skipButton.style.display = DisplayStyle.None;
                    _backButton.style.display = DisplayStyle.Flex;
                    CategoryChanged!(_bodyWearablesView.SelectedCategory);
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
            ShowStage(_currentStage = Stage.Preset);
            _configuratorContainer.SetVisibility(true);
            _loader.SetDisplay(false);
        }

        public void SetUsername(string username)
        {
            _username = username;
        }

        public void SetPresets(ProfileResponse.Avatar.AvatarData[] presets, int randomPresetIndex)
        {
            _presetsView.SetPresets(presets, randomPresetIndex);
        }

        public void SetCollection(List<CategoryDefinition> faceCollection, List<CategoryDefinition> bodyCollection)
        {
            /*
               Category: body_shape - 2

               Category: eyewear - 14
               Category: upper_body - 56
               Category: facial_hair - 13
               Category: lower_body - 38
               Category: feet - 24
               Category: hands_wear - 4
               Category: earring - 12
               Category: hair - 33
               Category: eyebrows - 26
               Category: eyes - 35
               Category: mouth - 20
             */

            _headWearablesView.SetCollection(faceCollection);
            _bodyWearablesView.SetCollection(bodyCollection);
        }

        public void SetBodyShape(BodyShape bodyShape)
        {
            _bodyShapePopupView.SetBodyShape(bodyShape);
        }

        public void SetSelectedItems(Dictionary<string, EntityDefinition> selectedItems)
        {
            _headWearablesView.SetSelectedItems(selectedItems);
            _bodyWearablesView.SetSelectedItems(selectedItems);
        }

        public void ClearPresetSelection()
        {
            _presetsView.ClearSelection();
        }

        private enum Stage
        {
            Preset,
            Face,
            Body
        }
    }
}