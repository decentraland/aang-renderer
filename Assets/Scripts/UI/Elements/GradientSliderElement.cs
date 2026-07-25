using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Elements
{
    /// <summary>
    /// A slider whose tracker is painted with a horizontal gradient built from arbitrary color stops.
    /// </summary>
    [UxmlElement]
    public partial class GradientSliderElement : Slider
    {
        private const string USS_BLOCK = "gradient-slider";
        private const string USS_GRADIENT = USS_BLOCK + "__gradient";
        private const int HUE_STOPS = 13;

        private readonly VisualElement _gradient;
        private Color[] _stops;

        public GradientSliderElement()
        {
            AddToClassList(USS_BLOCK);

            var tracker = this.Q(className: trackerUssClassName);
            _gradient = new VisualElement { name = "gradient", pickingMode = PickingMode.Ignore };
            _gradient.AddToClassList(USS_GRADIENT);
            _gradient.generateVisualContent += OnGenerateGradient;
            tracker.Add(_gradient);
        }

        public void SetGradient(params Color[] stops)
        {
            _stops = stops;
            _gradient.MarkDirtyRepaint();
        }

        public void SetHueGradient()
        {
            var stops = new Color[HUE_STOPS];
            for (var i = 0; i < HUE_STOPS; i++)
            {
                stops[i] = Color.HSVToRGB(i / (float)(HUE_STOPS - 1), 1f, 1f);
            }

            SetGradient(stops);
        }

        private void OnGenerateGradient(MeshGenerationContext mgc)
        {
            var rect = _gradient.contentRect;
            if (_stops == null || _stops.Length < 2 || rect.width <= 0f || rect.height <= 0f) return;

            var segments = _stops.Length - 1;
            var mesh = mgc.Allocate((segments + 1) * 2, segments * 6);

            for (var i = 0; i <= segments; i++)
            {
                var x = rect.xMin + rect.width * i / segments;
                Color32 tint = _stops[i];
                mesh.SetNextVertex(new Vertex { position = new Vector3(x, rect.yMin, Vertex.nearZ), tint = tint });
                mesh.SetNextVertex(new Vertex { position = new Vector3(x, rect.yMax, Vertex.nearZ), tint = tint });
            }

            for (var i = 0; i < segments; i++)
            {
                var tl = (ushort)(i * 2);
                var bl = (ushort)(i * 2 + 1);
                var tr = (ushort)(i * 2 + 2);
                var br = (ushort)(i * 2 + 3);
                mesh.SetNextIndex(tl);
                mesh.SetNextIndex(tr);
                mesh.SetNextIndex(br);
                mesh.SetNextIndex(tl);
                mesh.SetNextIndex(br);
                mesh.SetNextIndex(bl);
            }
        }
    }
}
