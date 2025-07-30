using UnityEngine.UIElements;

namespace UI.Views
{
    public abstract class StageView: IRefreshableView
    {
        private readonly VisualElement _root;

        public readonly string Title;
        public readonly string ConfirmButtonText;
        public readonly string ConfirmButtonTextMobile;
        public readonly int ConfirmButtonWidth; // This is ugly but the only way to get it to animate nicely
        public readonly bool CanSkip;

        public abstract string SelectedCategory { get; }

        protected bool UsingMobile;
        
        protected StageView(VisualElement root, string title, string confirmButtonText, int confirmButtonWidth, string confirmButtonTextMobile, bool canSkip)
        {
            _root = root;
            Title = title;
            ConfirmButtonText = confirmButtonText;
            ConfirmButtonTextMobile = confirmButtonTextMobile;
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

        public void Show()
        {
            _root.RemoveFromClassList("customization-window__content-item--hide-left");
            _root.RemoveFromClassList("customization-window__content-item--hide-right");
        }

        public virtual void SetUsingMobileMode(bool usingMobile)
        {
            UsingMobile = usingMobile;
        }

        public abstract object GetData();
        public abstract void SetData(object data);
    }
}