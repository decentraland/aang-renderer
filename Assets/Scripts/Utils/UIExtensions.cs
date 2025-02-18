using UnityEngine.UIElements;

namespace Utils
{
    public static class UIExtensions
    {
        public static void RotateBy(this VisualElement element, float angle)
        {
            element.style.rotate = new StyleRotate(new Rotate(new Angle(element.style.rotate.value.angle.value + angle)));
        }
    }
}