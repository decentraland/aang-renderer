using System;
using UI.Elements;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class BodyTypePopupView
    {
        private readonly PreviewButtonElement _maleBodyButton;
        private readonly PreviewButtonElement _femaleBodyButton;

        public bool IsMaleBody
        {
            get => _maleBodyButton.Selected;
            set
            {
                _maleBodyButton.Selected = value;
                _femaleBodyButton.Selected = !value;
            }
        }

        public event Action<bool> BodyTypeChanged;

        public BodyTypePopupView(VisualElement root)
        {
            _maleBodyButton = root.Q<PreviewButtonElement>("MaleBodyButton");
            _femaleBodyButton = root.Q<PreviewButtonElement>("FemaleBodyButton");

            IsMaleBody = true;

            _maleBodyButton.Clicked += OnMaleBodyClicked;
            _femaleBodyButton.Clicked += OnFemaleBodyClicked;
        }

        private void OnFemaleBodyClicked()
        {
            IsMaleBody = false;
            BodyTypeChanged?.Invoke(IsMaleBody);
        }

        private void OnMaleBodyClicked()
        {
            IsMaleBody = true;
            BodyTypeChanged?.Invoke(IsMaleBody);
        }
    }
}