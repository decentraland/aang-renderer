using System.Linq;
using UI.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

namespace UI.Elements
{
    [UxmlElement]
    public partial class DCLDropdownElement : VisualElement
    {
        private const string USS_BLOCK = "dcl-dropdown";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_ARROW = USS_BLOCK + "__arrow";
        private const string USS_LABEL = USS_BLOCK + "__label";
        private const string USS_CONTAINER = USS_BLOCK + "__container";

        public readonly VisualElement Icon;

        private readonly Label _label;

        [UxmlAttribute]
        public string Text
        {
            get => _label.text.Length > 0 ? _label.text[17..] : string.Empty;
            set
            {
                _label.SetDisplay(!string.IsNullOrEmpty(value));
                _label.text = "<font-weight=600>" + value;
            }
        }

        // TODO: Try to block this at runtime if it's set
        [UxmlAttribute]
        public bool ShowInEditor
        {
            get => contentContainer.style.display == DisplayStyle.Flex;
            set => contentContainer.SetDisplay(value);
        }

        private bool _showing;

        public override VisualElement contentContainer { get; }

        private VisualElement _popupRoot;
        private VisualElement _popup;

        public DCLDropdownElement()
        {
            AddToClassList(USS_BLOCK);
            AddToClassList("dcl-clickable");

            hierarchy.Add(_label = new Label { name = "label" });
            _label.AddToClassList(USS_LABEL);
            _label.SetDisplay(false);

            hierarchy.Add(Icon = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore });
            Icon.AddToClassList(USS_ICON);

            var arrow = new VisualElement { name = "arrow", pickingMode = PickingMode.Ignore };
            hierarchy.Add(arrow);
            arrow.AddToClassList(USS_ARROW);

            var container = new VisualElement { name = "container", pickingMode = PickingMode.Ignore };
            hierarchy.Add(container);
            container.AddToClassList(USS_CONTAINER);
            container.style.display = DisplayStyle.None;

            contentContainer = container;

            this.AddManipulator(new AudioClickable(Toggle));

            RegisterCallback<DetachFromPanelEvent, DCLDropdownElement>(static (_, e) => { e.Show(false); }, this);
        }

        private void Toggle()
        {
            Show(!_showing);
        }

        public void Show(bool show)
        {
            if (panel == null || show == _showing) return;

            if (show)
            {
                _popupRoot ??= panel.visualTree.Q("dcl-dropdown-popup-root");
                _popupRoot.RegisterCallbackOnce<PointerDownEvent, DCLDropdownElement>(static (_, e) => e.Show(false),
                    this);
                _popupRoot.RegisterCallback<GeometryChangedEvent>(RefreshPosition);
                _popupRoot.pickingMode = PickingMode.Position;

                _popup = contentContainer.Children().First();
                _popup.RemoveFromHierarchy();
                _popupRoot.Add(_popup);

                _popup.RegisterCallback<GeometryChangedEvent>(RefreshPosition);
            }
            else
            {
                _popupRoot.pickingMode = PickingMode.Ignore;
                _popupRoot.UnregisterCallback<GeometryChangedEvent>(RefreshPosition);
                _popup.UnregisterCallback<GeometryChangedEvent>(RefreshPosition);
                _popup.RemoveFromHierarchy();
                contentContainer.Add(_popup);
                _popup.style.left = StyleKeyword.Initial;
                _popup.style.top = StyleKeyword.Initial;
                _popup = null;
            }

            _showing = show;
        }

        private void RefreshPosition(GeometryChangedEvent _)
        {
            // Get the unscaled world bounds to ensure correct positioning even when element is scaled (e.g., during hover)
            var unscaledBounds = GetUnscaledWorldBounds();
            _popup.style.left = unscaledBounds.x - _popup.worldBound.width - _popup.resolvedStyle.marginRight +
                                unscaledBounds.width;
            _popup.style.top = unscaledBounds.y + unscaledBounds.height;
        }

        private Rect GetUnscaledWorldBounds()
        {
            var currentScale = transform.scale;
            var bounds = worldBound;

            if (Mathf.Approximately(currentScale.x, 1f) == false || Mathf.Approximately(currentScale.y, 1f) == false)
            {
                // Calculate the original size before scaling
                var unscaledWidth = bounds.width / currentScale.x;
                var unscaledHeight = bounds.height / currentScale.y;

                // Calculate the position adjustment due to scaling (elements scale from center)
                var widthDiff = (unscaledWidth - bounds.width) * 0.5f;
                var heightDiff = (unscaledHeight - bounds.height) * 0.5f;

                return new Rect(
                    bounds.x - widthDiff,
                    bounds.y - heightDiff,
                    unscaledWidth,
                    unscaledHeight
                );
            }

            return bounds;
        }
    }
}