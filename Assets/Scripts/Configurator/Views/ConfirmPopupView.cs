using System;
using UI.Elements;
using UnityEngine.UIElements;

namespace Configurator.Views
{
    public class ConfirmPopupView
    {
        private readonly VisualElement _root;
        public event Action Confirmed;

        public ConfirmPopupView(VisualElement root)
        {
            _root = root;
            root.Q<DCLButtonElement>("ConfirmButton").Clicked += OnConfirmClicked;
            root.Q<DCLButtonElement>("CancelButton").Clicked += OnCancelClicked;
        }

        private void OnCancelClicked()
        {
            Show(false);
        }

        private void OnConfirmClicked()
        {
            Confirmed!();
        }

        public void Show(bool show)
        {
            _root.EnableInClassList("confirmation-popup--hidden", !show);
        }
    }
}