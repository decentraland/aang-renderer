using System;
using Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Elements
{
    [UxmlElement]
    public partial class WearableItemElement : PreviewButtonElement
    {
        private const string USS_BLOCK = "wearable-preview-button";
        private const string USS_BLANK = USS_BLOCK + "--blank-{0}";

        public Action<WearableItemElement> WearableClicked { get; set; }

        public string WearableCategory { get; private set; }
        public ActiveEntity Wearable { get; private set; }

        private int textureRequestHandle = 0;

        public WearableItemElement()
        {
            AddToClassList(USS_BLOCK);

            Clicked += () => WearableClicked?.Invoke(this);
        }

        public void SetWearable(ActiveEntity wearable, string wearableCategory)
        {
            if (textureRequestHandle > 0)
            {
                RemoteTextureService.Instance.RemoveListener(textureRequestHandle);
                textureRequestHandle = 0;
            }

            if (Wearable == null && WearableCategory != null)
            {
                RemoveFromClassList(string.Format(USS_BLANK, WearableCategory));
            }

            Wearable = wearable;
            WearableCategory = wearableCategory;

            if (Wearable == null)
            {
                AddToClassList(string.Format(USS_BLANK, wearableCategory));
                SetTexture(null);
            }
            else
            {
                textureRequestHandle =
                    RemoteTextureService.Instance.RequestTexture(wearable.metadata.thumbnail, OnThumbnailLoaded,
                        OnThumbnailLoadError);
            }
        }

        private void OnThumbnailLoaded(Texture2D tex)
        {
            if (panel == null) return;

            SetTexture(tex);
            textureRequestHandle = 0;
        }

        private void OnThumbnailLoadError()
        {
            // TODO
            textureRequestHandle = 0;
        }
    }
}