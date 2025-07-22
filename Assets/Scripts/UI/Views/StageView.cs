using UnityEngine.UIElements;

namespace UI.Views
{
    public abstract class StageView
    {
        private readonly VisualElement _root;

        public readonly string Title;
        public readonly string ConfirmButtonText;
        public readonly int ConfirmButtonWidth; // This is ugly but the only way to get it to animate nicely
        public readonly bool CanSkip;

        public abstract string SelectedCategory { get; }


        protected StageView(VisualElement root, string title, string confirmButtonText, int confirmButtonWidth, bool canSkip)
        {
            _root = root;
            Title = title;
            ConfirmButtonText = confirmButtonText;
            CanSkip = canSkip;
            ConfirmButtonWidth = confirmButtonWidth;
        }

        public void HideLeft()
        {
            _root.AddToClassList("customization-window__content-item--hide-left");
        }

        public void HideRight()
        {
            _root.AddToClassList("customization-window__content-item--hide-right");
        }

        public virtual void Show()
        {
            _root.RemoveFromClassList("customization-window__content-item--hide-left");
            _root.RemoveFromClassList("customization-window__content-item--hide-right");
        }
    }
}