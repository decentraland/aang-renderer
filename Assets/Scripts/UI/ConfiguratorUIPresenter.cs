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
    [RequireComponent(typeof(UIDocument)), DefaultExecutionOrder(10)]
    public class ConfiguratorUIPresenter : MonoBehaviour
    {
        [SerializeField] private List<string> faceCategories;
        [SerializeField] private List<string> bodyCategories;

        // TODO: Maybe move to controller?
        [SerializeField] private Color[] presetSkinColors;
        [SerializeField] private Color[] presetHairColors;

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

        private Dictionary<string, List<ActiveEntity>> _collection;
        private List<(string category, List<ActiveEntity> entities)> _faceEntities;
        private List<(string category, List<ActiveEntity> entities)> _bodyEntities;

        // Views
        private WearablesView _headWearablesView;
        private WearablesView _bodyWearablesView;
        private PresetsView _presetsView;
        private BodyShapePopupView _bodyShapePopupView;
        private ColorPopupView _skinColorPopupView;
        private ColorPopupView _hairColorPopupView;

        private Stage _currentStage = Stage.Preset;

        public event Action<Vector2> CharacterAreaCenterChanged;
        public event Action<string> CategoryChanged;
        public event Action<Color> SkinColorSelected;
        public event Action<Color> HairColorSelected;
        public event Action<string> BodyShapeSelected;
        public event Action<string, ActiveEntity> WearableSelected;
        public event Action<ProfileResponse.Avatar.AvatarData> PresetSelected;

        private void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

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
            _presetsView.PresetSelected += preset => PresetSelected!(preset);

            // Dropdowns
            var bodyTypeDropdown = root.Q<DCLDropdownElement>("BodyTypeDropdown");
            _bodyShapePopupView = new BodyShapePopupView(bodyTypeDropdown.Q("BodyTypePopup"));
            _bodyShapePopupView.BodyShapeSelected += bs => BodyShapeSelected!(bs);
            
            var skinColorDropdown = root.Q<DCLDropdownElement>("SkinColorDropdown");
            _skinColorPopupView = new ColorPopupView(skinColorDropdown.Q("ColorPopup"), skinColorDropdown.Icon, presetSkinColors);
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
                headWearablesContainer.Q("Items"),
                categoryLocalizations
            );
            _headWearablesView.WearableSelected += (c, ae) => WearableSelected!(c, ae);
            _headWearablesView.CategoryChanged += c => CategoryChanged!(c);

            var bodyWearablesContainer = root.Q("BodyWearables");
            _bodyWearablesView = new WearablesView(
                bodyWearablesContainer,
                bodyWearablesContainer.Q<Label>("CategoryHeader"),
                bodyWearablesContainer.Q<VisualElement>("Sidebar"),
                bodyWearablesContainer.Q("Items"),
                categoryLocalizations
            );
            _bodyWearablesView.WearableSelected += (c, ae) => WearableSelected!(c, ae);
            _bodyWearablesView.CategoryChanged += c => CategoryChanged!(c);

            _configuratorContainer.SetDisplay(false);
            _loader.SetDisplay(true);
            ShowStage(_currentStage = Stage.Preset);
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
                    CategoryChanged!(null);
                    break;
                case Stage.Face:
                    _headWearablesView.Show(true);
                    _stageTitle.text = "2. Customize your face";
                    _confirmButton.Text = "CONFIRM FACE";
                    _skipButton.style.display = DisplayStyle.Flex;
                    _backButton.style.display = DisplayStyle.Flex;
                    CategoryChanged!(_headWearablesView.SelectedCategory);
                    break;
                case Stage.Body:
                    _bodyWearablesView.Show(true);
                    _stageTitle.text = "2. Customize your outfit";
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
            _configuratorContainer.SetDisplay(true);
            _loader.SetDisplay(false);
        }

        public void SetPresets(ProfileResponse.Avatar.AvatarData[] presets, int randomPresetIndex)
        {
            _presetsView.SetPresets(presets, randomPresetIndex);
        }

        public void SetCollection(Dictionary<string, List<ActiveEntity>> collection)
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

            _faceEntities = faceCategories.Select(cat => (cat, collection[cat].Prepend(null).ToList())).ToList();
            _bodyEntities = bodyCategories.Select(cat => (cat, collection[cat].Prepend(null).ToList())).ToList();

            _headWearablesView.SetCollection(_faceEntities);
            _bodyWearablesView.SetCollection(_bodyEntities);
        }

        public void SetBodyShape(string bodyShape)
        {
            _bodyShapePopupView.SetBodyShape(bodyShape);
        }

        public void SetSelectedItems(Dictionary<string, ActiveEntity> selectedItems)
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