using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UI.Elements;
using UnityEngine.UIElements;
using Utils;

namespace UI.Views
{
    public class WearablesView : UIView
    {
        private readonly Label _header;
        private readonly VisualElement _sidebar;
        private readonly VisualElement _itemsContainer;
        private readonly Dictionary<string, string> _categoryLocalizations;

        private List<(string category, List<ActiveEntity> wearables)> _collection;

        private Dictionary<string, WearableCategoryElement> _categoryElements = new();
        private WearableCategoryElement _selectedCategoryElement;
        private WearableItemElement _selectedWearableElement;
        private readonly Dictionary<string, ActiveEntity> _selectedItems = new();
        
        public string SelectedCategory => _selectedCategoryElement.Category;
        public event Action<string> CategoryChanged;
        public event Action<string, ActiveEntity> WearableSelected;

        public WearablesView(VisualElement root, Label header, VisualElement sidebar, VisualElement itemsContainer,
            Dictionary<string, string> categoryLocalizations) : base(root)
        {
            _header = header;
            _sidebar = sidebar;
            _itemsContainer = itemsContainer;
            _categoryLocalizations = categoryLocalizations;

            foreach (var ve in _itemsContainer.Children())
            {
                ((WearableItemElement)ve).WearableClicked = OnWearableClicked;
            }
        }

        public void SetCollection(List<(string category, List<ActiveEntity> wearables)> collection)
        {
            _collection = collection;
            _sidebar.Clear();

            var categorySet = false;
            foreach (var (category, _) in collection)
            {
                var categoryElement = new WearableCategoryElement(category);
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

        public void SetSelectedItems(Dictionary<string, ActiveEntity> selectedItems)
        {
            foreach (var category in _selectedItems.Keys.ToList())
            {
                _selectedItems[category] = selectedItems.GetValueOrDefault(category);
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

            _header.text = _categoryLocalizations[category];

            var categoryItems = _collection.First(cw => cw.category == category).wearables;
            var selectedWearable = _selectedItems[category];

            var index = 0;
            foreach (var ve in _itemsContainer.Children())
            {
                if (index == 0)
                {
                    ve.SetDisplay(true);
                    var wpbe = (WearableItemElement)ve;
                    wpbe.SetWearable(null, category);
                }
                else if (index < categoryItems.Count)
                {
                    var wearable = categoryItems[index];

                    ve.SetDisplay(true);
                    var wpbe = (WearableItemElement)ve;
                    wpbe.SetWearable(categoryItems[index], category);
                    wpbe.Selected = wearable == selectedWearable;
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