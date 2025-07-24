using System;
using System.Collections.Generic;
using UI.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class ColorPopupView
    {
        public event Action<Color> ColorSelected;

        private readonly VisualElement _icon;
        private readonly VisualElement _container;
        private readonly Label _title;

        public ColorPopupView(VisualElement root, VisualElement icon)
        {
            _icon = icon;
            _container = root.Q("PresetsContainer");
            _title = root.Q<Label>("Title");
        }

        public void SetTitle(string title)
        {
            _title.text = $"<font-weight=600>{title}";
        }

        public void SetColors(Color[] colors)
        {
            _container.Clear();

            foreach (var color in colors)
            {
                var preset = new VisualElement();
                preset.AddToClassList("popup-color__preset");
                preset.AddToClassList("dcl-clickable");
                preset.style.backgroundColor = color;
                preset.AddManipulator(new AudioClickable(() => OnColorSelected(color)));

                _container.Add(preset);
            }
        }

        public void SetSelectedColor(Color color)
        {
            _icon.style.backgroundColor = color;
        }

        private void OnColorSelected(Color color)
        {
            _icon.style.backgroundColor = color;
            ColorSelected!(color);
        }
    }
}