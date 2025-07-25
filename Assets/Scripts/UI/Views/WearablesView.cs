using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UI.Elements;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

namespace UI.Views
{
    public class WearablesView : StageView
    {
        private readonly Label _header;
        private readonly VisualElement _sidebar;
        private readonly VisualElement _itemsContainer;
        private readonly DCLDropdownElement _colorDropdown;

        private List<CategoryDefinition> _collection;

        private readonly Dictionary<string, WearableCategoryElement> _categoryElements = new();
        private WearableCategoryElement _selectedCategoryElement;
        private WearableItemElement _selectedWearableElement;
        private readonly Dictionary<string, EntityDefinition> _selectedItems = new();

        private readonly ColorPopupView _colorPopupView;

        public override string SelectedCategory => _selectedCategoryElement.Category;

        public event Action<string> CategoryChanged;
        public event Action<string, EntityDefinition> WearableSelected;
        public event Action<Color> ColorSelected;

        private Color[] _hairColorPresets;
        private Color[] _eyeColorPresets;
        private Color _currentHairColor;
        private Color _currentEyeColor;

        public WearablesView(VisualElement root, string title, string confirmButtonText, int confirmButtonWidth,
            string confirmButtonTextMobile, bool canSkip) : base(root, title, confirmButtonText, confirmButtonWidth,
            confirmButtonTextMobile, canSkip)
        {
            _header = root.Q<Label>("CategoryHeader");
            _sidebar = root.Q<VisualElement>("Sidebar");
            _itemsContainer = root.Q("Items");

            _colorDropdown = root.Q<DCLDropdownElement>("ColorDropdown");
            _colorPopupView = new ColorPopupView(_colorDropdown.Q("ColorPopup"), _colorDropdown.Icon);
            _colorPopupView.ColorSelected += OnColorSelected;

            foreach (var ve in _itemsContainer.Children())
            {
                ((WearableItemElement)ve).WearableClicked = OnWearableClicked;
            }
        }

        public void SetCollection(List<CategoryDefinition> collection)
        {
            _collection = collection;
            _sidebar.Clear();

            var categorySet = false;
            foreach (var cd in collection)
            {
                var categoryElement = new WearableCategoryElement(cd.id, cd.title, cd.emptyThumbnail);
                categoryElement.Clicked += OnCategoryClicked;
                _sidebar.Add(categoryElement);
                _categoryElements[cd.id] = categoryElement;
                _selectedItems[cd.id] = null;

                if (categorySet) continue;

                categorySet = true;
                _selectedCategoryElement = categoryElement;
                _selectedCategoryElement.SetSelected(true);
                RefreshCurrentCategory();
            }
        }

        public void SetSelectedItems(Dictionary<string, EntityDefinition> selectedItems)
        {
            foreach (var category in _selectedItems.Keys.ToList())
            {
                var selectedItem = selectedItems.GetValueOrDefault(category);
                _selectedItems[category] = selectedItem;
                _categoryElements[category].SetWearable(selectedItem);
            }

            RefreshCurrentCategory();
        }

        private void OnCategoryClicked(WearableCategoryElement categoryElement)
        {
            _selectedCategoryElement?.SetSelected(false);
            _selectedCategoryElement = categoryElement;
            _selectedCategoryElement.SetSelected(true);

            RefreshCurrentCategory();

            CategoryChanged!(categoryElement.Category);
        }

        private void RefreshCurrentCategory()
        {
            var category = _selectedCategoryElement.Category;

            // Ugly but ok
            if (category == WearablesConstants.Categories.EYES)
            {
                _colorDropdown.SetVisibility(true);
                _colorPopupView.SetColors(_eyeColorPresets);
                _colorPopupView.SetSelectedColor(_currentEyeColor);
                _colorPopupView.SetTitle(_colorDropdown.Text = "EYE COLOR");
            }
            else if (category == WearablesConstants.Categories.HAIR)
            {
                _colorDropdown.SetVisibility(true);
                _colorPopupView.SetColors(_hairColorPresets);
                _colorPopupView.SetSelectedColor(_currentHairColor);
                _colorPopupView.SetTitle(_colorDropdown.Text = "HAIR COLOR");
            }
            else
            {
                _colorDropdown.SetVisibility(false);
            }

            _header.text = "<font-weight=600>" + _selectedCategoryElement.Title;

            var categoryDefinition = _collection.First(cd => cd.id == category);
            var selectedWearable = _selectedItems[category];

            if (categoryDefinition.Definitions.Count > 20)
            {
                Debug.LogError($"Too many items in {category}: {categoryDefinition.Definitions.Count}");
            }

            var index = 0;
            foreach (var ve in _itemsContainer.Children())
            {
                if (index < categoryDefinition.Definitions.Count)
                {
                    var wearable = categoryDefinition.Definitions[index];

                    ve.SetVisibility(true);
                    var wpbe = (WearableItemElement)ve;
                    wpbe.EmptyTexture = categoryDefinition.emptyThumbnail;
                    wpbe.SetWearable(wearable);
                    var selected = wearable == selectedWearable;
                    wpbe.Selected = selected;

                    if (selected)
                    {
                        _selectedWearableElement = wpbe;
                    }
                }
                else
                {
                    ve.SetVisibility(false);
                }

                index++;
            }
        }

        public void SetColorPresets(Color[] hairColorPresets, Color[] eyeColorPresets)
        {
            _hairColorPresets = hairColorPresets;
            _eyeColorPresets = eyeColorPresets;
        }

        public void SetSelectedColors(Color hairColor, Color eyeColor)
        {
            _currentHairColor = hairColor;
            _currentEyeColor = eyeColor;

            var category = _selectedCategoryElement?.Category;
            if (category == WearablesConstants.Categories.HAIR)
            {
                _colorPopupView.SetSelectedColor(hairColor);
            }
            else if (category == WearablesConstants.Categories.EYES)
            {
                _colorPopupView.SetSelectedColor(eyeColor);
            }
        }

        private void OnColorSelected(Color color)
        {
            var category = _selectedCategoryElement.Category;
            if (category == WearablesConstants.Categories.HAIR)
            {
                _currentHairColor = color;
            }
            else if (category == WearablesConstants.Categories.EYES)
            {
                _currentEyeColor = color;
            }

            ColorSelected!(color);
        }

        private void OnWearableClicked(WearableItemElement wpbe)
        {
            if (_selectedWearableElement != null) _selectedWearableElement.Selected = false;
            wpbe.Selected = true;
            _selectedWearableElement = wpbe;

            _selectedItems[_selectedCategoryElement.Category] = wpbe.Wearable;

            _selectedCategoryElement.SetWearable(wpbe.Wearable);

            WearableSelected!(_selectedCategoryElement.Category, wpbe.Wearable);
        }
    }
}