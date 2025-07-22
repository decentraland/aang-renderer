using System;
using UI.Elements;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class ConfirmPopupView : UIView
    {
        public event Action Confirmed;
        public event Action Cancelled;

        public ConfirmPopupView(VisualElement root) : base(root)
        {
            root.Q<DCLButtonElement>("ConfirmButton").Clicked += OnConfirmClicked;
            root.Q<DCLButtonElement>("CancelButton").Clicked += OnCancelClicked;
        }

        private void OnCancelClicked()
        {
            Cancelled!();
        }

        private void OnConfirmClicked()
        {
            Confirmed!();
        }

        public override void Show(bool show)
        {
            Root.EnableInClassList("confirmation-popup--hidden", !show);
        }
    }
}