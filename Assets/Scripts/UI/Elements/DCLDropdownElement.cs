using UnityEngine.UIElements;

namespace UI.Elements
{
    [UxmlElement]
    public partial class DCLDropdownElement : VisualElement
    {
        private const string USS_BLOCK = "dcl-dropdown";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_ARROW = USS_BLOCK + "__arrow";
        private const string USS_CONTAINER = USS_BLOCK + "__container";
        private const string USS_CONTAINER_HIDDEN = USS_CONTAINER + "--hidden";

        public readonly VisualElement Icon;
        
        // TODO: Wat
        [UxmlAttribute]
        public bool Hidden
        {
            get => contentContainer.ClassListContains(USS_CONTAINER_HIDDEN);
            set => contentContainer.EnableInClassList(USS_CONTAINER_HIDDEN, value);
        }

        public override VisualElement contentContainer { get; }

        public DCLDropdownElement()
        {
            AddToClassList(USS_BLOCK);

            hierarchy.Add(Icon = new VisualElement {name = "icon"});
            Icon.AddToClassList(USS_ICON);
            
            var arrow = new VisualElement {name = "arrow"};
            hierarchy.Add(arrow);
            arrow.AddToClassList(USS_ARROW);
            
            var container = new VisualElement {name = "container"};
            hierarchy.Add(container);
            container.AddToClassList(USS_CONTAINER);
            container.AddToClassList(USS_CONTAINER_HIDDEN);

            contentContainer = container;
            
            this.AddManipulator(new Clickable(() => Hidden = !Hidden));
        }
    }
}