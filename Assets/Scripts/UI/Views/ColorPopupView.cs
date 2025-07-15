using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class ColorPopupView
    {
        public event Action<Color> ColorSelected;

        private readonly VisualElement _icon;

        public ColorPopupView(VisualElement root, VisualElement icon, Color[] colors)
        {
            _icon = icon;

            var presetsContainer = root.Q("PresetsContainer");
            presetsContainer.Clear();

            foreach (var color in colors)
            {
                var preset = new VisualElement();
                preset.AddToClassList("popup-color__preset");
                preset.AddToClassList("dcl-clickable");
                preset.style.backgroundColor = color;
                preset.AddManipulator(new Clickable(() => OnColorSelected(color)));
                
                presetsContainer.Add(preset);
            }
        }

        private void OnColorSelected(Color color)
        {
            _icon.style.backgroundColor = color;
            ColorSelected!(color);
        }
    }
}