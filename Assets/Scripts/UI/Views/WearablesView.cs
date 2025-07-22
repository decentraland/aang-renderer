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

        private List<CategoryDefinition> _collection;

        private Dictionary<string, WearableCategoryElement> _categoryElements = new();
        private WearableCategoryElement _selectedCategoryElement;
        private WearableItemElement _selectedWearableElement;
        private readonly Dictionary<string, EntityDefinition> _selectedItems = new();

        public override string SelectedCategory => _selectedCategoryElement.Category;

        public event Action<string> CategoryChanged;
        public event Action<string, EntityDefinition> WearableSelected;

        public WearablesView(VisualElement root, string title, string confirmButtonText, int confirmButtonWidth,
            bool canSkip) : base(root, title, confirmButtonText, confirmButtonWidth, canSkip)
        {
            _header = root.Q<Label>("CategoryHeader");
            _sidebar = root.Q<VisualElement>("Sidebar");
            _itemsContainer = root.Q("Items");

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

                    ve.SetDisplay(true);
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
                    ve.style.display = DisplayStyle.None;
                }

                index++;
            }
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