using System;
using System.Collections.Generic;
using Data;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class ConfiguratorUIPresenter: MonoBehaviour
    {
        private TabView categoriesTabView;
        private Tab upperBodyTab;
        private Tab lowerBodyTab;
        private Tab feetTab;
        private Tab eyewearTab;
        private Tab handsTab;
        private Tab earringsTab;

        private VisualElement _loader;
        private VisualElement _loaderIcon;
        
        private DropdownField bodyShapeDropdown;

        public string BodyShape => bodyShapeDropdown.value;
        public Dictionary<string, WearableDefinition> Setup { get; } = new ();

        public event Action SetupChanged;
        
        private void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            categoriesTabView = root.Q<TabView>("CategoriesTabView");

            upperBodyTab = categoriesTabView.Q<Tab>("UpperBodyTab");
            lowerBodyTab = categoriesTabView.Q<Tab>("LowerBodyTab");
            feetTab = categoriesTabView.Q<Tab>("FeetTab");
            eyewearTab = categoriesTabView.Q<Tab>("EyewearTab");
            handsTab = categoriesTabView.Q<Tab>("HandsTab");
            earringsTab = categoriesTabView.Q<Tab>("EarringsTab");

            categoriesTabView.activeTabChanged += OnActiveTabChanged;

            upperBodyTab.selected += OnUpperBodySelected;
            
            bodyShapeDropdown = root.Q<DropdownField>("BodyShapeDropdown");
            bodyShapeDropdown.RegisterValueChangedCallback(evt => SetupChanged?.Invoke());

            _loader = root.Q("Loader");
            _loaderIcon = _loader.Q("Icon");
        }

        private void OnUpperBodySelected(Tab obj)
        {
            Debug.Log("OnUpperBodySelected");
        }

        private void OnActiveTabChanged(Tab previous, Tab next)
        {
            Debug.Log($"Selected tab prev: {previous.label},  next: {next.label}");
        }

        private void Update()
        {
            // Rotate the loader icon
            _loaderIcon.RotateBy(360f * Time.deltaTime);

        }

        public void ShowLoading(bool loading)
        {
            
        }

        public void SetCollection(Dictionary<string, List<ActiveEntity>> collection)
        {
            /*
               Category: eyewear - 14
               Category: upper_body - 56
               Category: facial_hair - 13
               Category: body_shape - 2
               Category: lower_body - 38
               Category: feet - 24
               Category: hands_wear - 4
               Category: tiara - 5
               Category: earring - 12
               Category: hair - 33
               Category: eyebrows - 26
               Category: eyes - 35
               Category: mouth - 20
             */
            
            SetupTab(upperBodyTab, collection["upper_body"]);
            SetupTab(lowerBodyTab, collection["lower_body"]);
            SetupTab(feetTab, collection["feet"]);
            SetupTab(eyewearTab, collection["eyewear"]);
            SetupTab(handsTab, collection["hands_wear"]);
            SetupTab(earringsTab, collection["earring"]);
        }

        private void SetupTab(Tab tab, List<ActiveEntity> entities)
        {
            var container = tab.Q<ScrollView>();
            
            foreach (var activeEntity in entities)
            {
                var item = new VisualElement();
                item.AddToClassList("wearable-item");
                item.AddManipulator(new Clickable(() => SetItem(activeEntity)));
                item.style.height = 70;
                item.style.width = 70;
                item.style.backgroundColor = Color.green;
                
                RemoteTextureService.Instance.RequestTexture(activeEntity.metadata.thumbnail, tex => item.style.backgroundImage = tex, () => Debug.LogError("RECEIVED ERROR"));
                
                container.Add(item);
            }
            
            tab.contentContainer.Add(container);
        }

        private void SetItem(ActiveEntity ae)
        {
            var definition = WearableDefinition.FromActiveEntity(ae, bodyShapeDropdown.value);

            Setup[definition.Category] = definition;
            
            SetupChanged?.Invoke();
        }

    }
}