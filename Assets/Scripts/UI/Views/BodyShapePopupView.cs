using System;
using Data;
using UI.Elements;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class BodyShapePopupView
    {
        private readonly PreviewButtonElement _maleBodyButton;
        private readonly PreviewButtonElement _femaleBodyButton;

        public event Action<BodyShape> BodyShapeSelected;

        public BodyShapePopupView(VisualElement root)
        {
            _maleBodyButton = root.Q<PreviewButtonElement>("MaleBodyButton");
            _femaleBodyButton = root.Q<PreviewButtonElement>("FemaleBodyButton");

            _maleBodyButton.Clicked += OnMaleBodyClicked;
            _femaleBodyButton.Clicked += OnFemaleBodyClicked;
        }

        public void SetBodyShape(BodyShape bodyShape)
        {
            _femaleBodyButton.Selected = bodyShape == BodyShape.Female;
            _maleBodyButton.Selected = bodyShape == BodyShape.Male;
        }

        private void OnFemaleBodyClicked()
        {
            _femaleBodyButton.Selected = true;
            _maleBodyButton.Selected = false;
            BodyShapeSelected!(BodyShape.Female);
        }

        private void OnMaleBodyClicked()
        {
            _femaleBodyButton.Selected = false;
            _maleBodyButton.Selected = true;
            BodyShapeSelected!(BodyShape.Male);
        }
    }
}