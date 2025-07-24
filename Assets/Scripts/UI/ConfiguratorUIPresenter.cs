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

        private StageView[] _stages;
        private int _currentStageIndex;

        public event Action<Vector2> CharacterAreaCenterChanged;
        public event Action<float> CharacterAreaZoom;
        public event Action<Vector2> CharacterAreaDrag;

        public event Action<string> CategoryChanged;
        public event Action<Color> SkinColorSelected;
        public event Action<Color> HairColorSelected;
        public event Action<Color> EyeColorSelected;
        public event Action<BodyShape> BodyShapeSelected;
        public event Action<string, EntityDefinition> WearableSelected;
        public event Action<PresetDefinition> PresetSelected;

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
            _skipButton.Clicked += () => Confirmed!();

            _loader = root.Q("Loader");
            _loaderIcon = _loader.Q("Icon");

            var presetsContainer = root.Q("Presets");
            _presetsView = new PresetsView(presetsContainer,
                "1. Choose {0}'s starting look",
                "START CUSTOMIZING",
                212,
                true);
            _presetsView.PresetSelected += preset => PresetSelected!(preset);

            // Dropdowns
            var bodyTypeDropdown = root.Q<DCLDropdownElement>("BodyTypeDropdown");
            _bodyShapePopupView = new BodyShapePopupView(bodyTypeDropdown.Q("BodyTypePopup"));
            _bodyShapePopupView.BodyShapeSelected += bs => BodyShapeSelected!(bs);

            var skinColorDropdown = root.Q<DCLDropdownElement>("SkinColorDropdown");
            _skinColorPopupView = new ColorPopupView(skinColorDropdown.Q("ColorPopup"), skinColorDropdown.Icon);
            _skinColorPopupView.ColorSelected += skinColor => SkinColorSelected!(skinColor);

            _headWearablesView = new WearablesView(
                root.Q("HeadWearables"),
                "2. Customize {0}'s face",
                "CONFIRM FACE",
                170,
                true);
            _headWearablesView.WearableSelected += (c, ae) => WearableSelected!(c, ae);
            _headWearablesView.CategoryChanged += c => CategoryChanged!(c);
            _headWearablesView.ColorSelected += c =>
            {
                switch (_headWearablesView.SelectedCategory)
                {
                    case WearablesConstants.Categories.EYES:
                        EyeColorSelected!(c);
                        break;
                    case WearablesConstants.Categories.HAIR:
                        HairColorSelected!(c);
                        break;
                }
            };

            _bodyWearablesView = new WearablesView(
                root.Q("BodyWearables"),
                "2. Customize {0}'s outfit",
                "FINISH",
                114,
                false);
            _bodyWearablesView.WearableSelected += (c, ae) => WearableSelected!(c, ae);
            _bodyWearablesView.CategoryChanged += c => CategoryChanged!(c);

            _confirmPopupView = new ConfirmPopupView(root.Q("ConfirmationPopup"));
            _confirmPopupView.Confirmed += () => Confirmed!();

            _stages = new StageView[]
            {
                _presetsView,
                _headWearablesView,
                _bodyWearablesView
            };

            RefreshCurrentStage();
            _configuratorContainer.SetVisibility(false);
            _loader.SetDisplay(true);
        }

        private void OnBackClicked()
        {
            if (_currentStageIndex == 0) return;

            _stages[_currentStageIndex].HideRight();
            _stages[--_currentStageIndex].Show();

            RefreshCurrentStage();
        }

        private void OnNextClicked()
        {
            if (_currentStageIndex == _stages.Length - 1)
            {
                Confirmed!();
                return;
            }

            _stages[_currentStageIndex].HideLeft();
            _stages[++_currentStageIndex].Show();

            RefreshCurrentStage();
        }

        private void RefreshCurrentStage()
        {
            var stage = _stages[_currentStageIndex];
            _stageTitle.text = string.Format(stage.Title, _username);
            _confirmButton.Text = stage.ConfirmButtonText;
            _confirmButton.style.width = stage.ConfirmButtonWidth;

            _skipButton.EnableInClassList("dcl-button--hidden-down", !stage.CanSkip);
            _backButton.EnableInClassList("dcl-button--hidden-down", _currentStageIndex == 0);

            CategoryChanged!(stage.SelectedCategory);
        }

        private void Update()
        {
            // Rotate the loader icon
            _loaderIcon.RotateBy(360f * Time.deltaTime);
        }

        public void LoadCompleted()
        {
            RefreshCurrentStage();
            _configuratorContainer.SetVisibility(true);
            _loader.SetDisplay(false);
        }

        public void SetUsername(string username)
        {
            _username = username;
        }

        public void SetAvatarPresets(PresetDefinition[] avatarPresets, int selectedAvatarPresetIndex)
        {
            _presetsView.SetPresets(avatarPresets, selectedAvatarPresetIndex);
        }

        public void SetColorPresets(Color[] skinColorPresets, Color[] hairColorPresets, Color[] eyeColorPresets)
        {
            _skinColorPopupView.SetColors(skinColorPresets);
            _headWearablesView.SetColorPresets(hairColorPresets, eyeColorPresets);
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

            _bodyWearablesView.SetCollection(bodyCollection);
            _headWearablesView.SetCollection(faceCollection);
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

        public void SetColors(Color skinColor, Color hairColor, Color eyeColor)
        {
            _skinColorPopupView.SetSelectedColor(skinColor);
            _headWearablesView.SetSelectedColors(hairColor, eyeColor);
        }

        public void ClearPresetSelection()
        {
            _presetsView.ClearSelection();
        }
    }
}