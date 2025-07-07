using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class ConfiguratorUIPresenter: MonoBehaviour
    {
        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
        }
    }
}