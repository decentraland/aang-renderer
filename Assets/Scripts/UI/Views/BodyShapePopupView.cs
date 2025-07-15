using System;
using UI.Elements;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class BodyShapePopupView
    {
        private readonly PreviewButtonElement _maleBodyButton;
        private readonly PreviewButtonElement _femaleBodyButton;

        public event Action<string> BodyShapeSelected;

        public BodyShapePopupView(VisualElement root)
        {
            _maleBodyButton = root.Q<PreviewButtonElement>("MaleBodyButton");
            _femaleBodyButton = root.Q<PreviewButtonElement>("FemaleBodyButton");

            _maleBodyButton.Clicked += OnMaleBodyClicked;
            _femaleBodyButton.Clicked += OnFemaleBodyClicked;
        }

        public void SetBodyShape(string bodyShape)
        {
            _femaleBodyButton.Selected = bodyShape == WearablesConstants.BODY_SHAPE_FEMALE;
            _maleBodyButton.Selected = bodyShape == WearablesConstants.BODY_SHAPE_MALE;
        }

        private void OnFemaleBodyClicked()
        {
            _femaleBodyButton.Selected = true;
            _maleBodyButton.Selected = false;
            BodyShapeSelected!(WearablesConstants.BODY_SHAPE_FEMALE);
        }

        private void OnMaleBodyClicked()
        {
            _femaleBodyButton.Selected = false;
            _maleBodyButton.Selected = true;
            BodyShapeSelected!(WearablesConstants.BODY_SHAPE_MALE);
        }
    }
}