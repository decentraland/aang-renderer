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
    public class WearablesView : UIView
    {
        private readonly Label _header;
        private readonly VisualElement _sidebar;
        private readonly VisualElement _itemsContainer;

        private List<(string category, string title, Texture2D defaultThumbnail, List<EntityDefinition> wearables)> _collection;

        private Dictionary<string, WearableCategoryElement> _categoryElements = new();
        private WearableCategoryElement _selectedCategoryElement;
        private WearableItemElement _selectedWearableElement;
        private readonly Dictionary<string, EntityDefinition> _selectedItems = new();

        public string SelectedCategory => _selectedCategoryElement.Category;
        public event Action<string> CategoryChanged;
        public event Action<string, EntityDefinition> WearableSelected;

        public WearablesView(VisualElement root, Label header, VisualElement sidebar, VisualElement itemsContainer) : base(root)
        {
            _header = header;
            _sidebar = sidebar;
            _itemsContainer = itemsContainer;

            foreach (var ve in _itemsContainer.Children())
            {
                ((WearableItemElement)ve).WearableClicked = OnWearableClicked;
            }
        }

        public void SetCollection(List<(string category, string title, Texture2D defaultThumbnail, List<EntityDefinition>)> collection)
        {
            _collection = collection;
            _sidebar.Clear();

            var categorySet = false;
            foreach (var (category, title, defaultThumbnail, _) in collection)
            {
                var categoryElement = new WearableCategoryElement(category, title, defaultThumbnail);
                categoryElement.Clicked += OnCategoryClicked;
                _sidebar.Add(categoryElement);
                _categoryElements[category] = categoryElement;
                _selectedItems[category] = null;

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

            _header.text = _selectedCategoryElement.Title;

            var categoryDefinition = _collection.First(cw => cw.category == category);
            var selectedWearable = _selectedItems[category];

            if (categoryDefinition.wearables.Count > 20)
            {
                Debug.LogError($"Too many items in {category}: {categoryDefinition.wearables.Count}");
            }

            var index = 0;
            foreach (var ve in _itemsContainer.Children())
            {
                if (index < categoryDefinition.wearables.Count)
                {
                    var wearable = categoryDefinition.wearables[index];

                    ve.SetDisplay(true);
                    var wpbe = (WearableItemElement)ve;
                    wpbe.EmptyTexture = categoryDefinition.defaultThumbnail;
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