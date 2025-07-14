using System;
using Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Elements
{
    [UxmlElement]
    public partial class PreviewButtonElement: VisualElement
    {
        private const string USS_BLOCK = "preview-button";
        private const string USS_CONTAINER = USS_BLOCK + "__container";
        private const string USS_SELECTED = USS_BLOCK + "--selected";

        public bool Selected
        {
            get => ClassListContains(USS_SELECTED);
            set => EnableInClassList(USS_SELECTED, value);
        }
        
        public event Action Clicked;
        
        private readonly VisualElement _container;

        private Clickable _clickable;

        public PreviewButtonElement()
        {
            AddToClassList(USS_BLOCK);
            AddToClassList("dcl-clickable");
            
            Add(_container = new VisualElement {name = "container"});
            _container.AddToClassList(USS_CONTAINER);
            
            this.AddManipulator(_clickable = new Clickable(() => Clicked?.Invoke()));
        }

        public void RemoveClickable()
        {
            this.RemoveManipulator(_clickable);
            RemoveFromClassList("dcl-clickable");
            _clickable = null;
        }

        public void SetTexture(Texture2D texture)
        {
            _container.style.backgroundImage = texture;
        }
    }
}