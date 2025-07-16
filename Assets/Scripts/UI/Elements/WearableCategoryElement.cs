using System;
using Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Elements
{
    [UxmlElement]
    public partial class WearableCategoryElement : VisualElement
    {
        private const string USS_BLOCK = "wearable-category";
        private const string USS_SELECTED = USS_BLOCK + "--selected";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_ICON_CATEGORY = USS_BLOCK + "__icon--{0}";
        private const string USS_THUMBNAIL_CONTAINER = USS_BLOCK + "__thumbnail-container";
        private const string USS_THUMBNAIL = USS_BLOCK + "__thumbnail";

        private readonly VisualElement _icon;
        private readonly WearableItemElement _thumbnail;

        public readonly string Category;

        public event Action<WearableCategoryElement> Clicked;

        public WearableCategoryElement() : this("upper_body")
        {
        }

        public WearableCategoryElement(string category)
        {
            Category = category;

            AddToClassList(USS_BLOCK);
            AddToClassList("dcl-clickable");

            Add(_icon = new VisualElement { name = "icon" });
            _icon.AddToClassList(USS_ICON);
            _icon.AddToClassList(string.Format(USS_ICON_CATEGORY, category));

            // var thumbnailContainer = new VisualElement { name = "thumbnail-container" };
            // Add(thumbnailContainer);
            // thumbnailContainer.AddToClassList(USS_THUMBNAIL_CONTAINER);
            // {
            // }

            Add(_thumbnail = new WearableItemElement { name = "thumbnail" });
            _thumbnail.AddToClassList(USS_THUMBNAIL);
            _thumbnail.RemoveClickable();
            this.AddManipulator(new Clickable(() => Clicked?.Invoke(this)));
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList(USS_SELECTED, selected);
        }

        public void SetWearable(EntityDefinition entity)
        {
            _thumbnail.SetWearable(entity, Category);
        }
    }
}