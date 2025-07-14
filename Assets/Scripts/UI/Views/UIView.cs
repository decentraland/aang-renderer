using UnityEngine.UIElements;

namespace UI.Views
{
    public abstract class UIView
    {
        private readonly VisualElement _root;

        public UIView(VisualElement root)
        {
            _root = root;
        }

        public void Show(bool show)
        {
            _root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}