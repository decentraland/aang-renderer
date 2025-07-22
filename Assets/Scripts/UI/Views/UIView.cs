using UnityEngine.UIElements;

namespace UI.Views
{
    public abstract class UIView
    {
        protected readonly VisualElement Root;

        public UIView(VisualElement root)
        {
            Root = root;
        }

        public virtual void Show(bool show)
        {
            Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}