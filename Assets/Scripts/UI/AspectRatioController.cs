using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace UI
{
    public class AspectRatioController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        [SerializeField] private Mode mode = Mode.LandscapeMatch;

        [SerializeField] private bool useRuntimeMatch = false;
        [SerializeField, Range(0, 1)] private float match;

        [SerializeField] private float portraitMatchLimit = 1f;
        [SerializeField] private float portraitForceLimit = 2f;
        
        [SerializeField] private Vector2Int landscapeMatchResolution;
        [SerializeField] private Vector2Int portraitMatchResolution;
        [SerializeField] private Vector2Int portraitForceResolution;

        private VisualElement _root;

        private void Start()
        {
            _root = uiDocument.rootVisualElement.Q("root");
        }

        private void Update()
        {
            var width = Screen.width;
            var height = Screen.height;

            var aspectRatio = (float)width / height;

            Mode newMode;

            if (aspectRatio > portraitMatchLimit)
            {
                newMode = Mode.LandscapeMatch;
            }
            else if (aspectRatio > portraitForceLimit)
            {
                newMode = Mode.PortraitMatch;
            }
            else
            {
                newMode = Mode.PortraitForce;
            }

            TryUpdateMode(newMode);
        }

        private void TryUpdateMode(Mode newMode)
        {
            if (mode == newMode) return;

            switch (newMode)
            {
                case Mode.LandscapeMatch:
                    Debug.Log("Switching to Landscape mode");
                    uiDocument.panelSettings.match = 1f;
                    uiDocument.panelSettings.referenceResolution = landscapeMatchResolution;
                    _root.EnableInClassList("portrait", false);
                    break;
                case Mode.PortraitMatch:
                    Debug.Log("Switching to PortraitMatch mode");
                    uiDocument.panelSettings.match = 0f;
                    uiDocument.panelSettings.referenceResolution = portraitMatchResolution;
                    _root.EnableInClassList("portrait", false);
                    break;
                case Mode.PortraitForce:
                    Debug.Log("Switching to PortraitForce mode");
                    uiDocument.panelSettings.match = 0f;
                    uiDocument.panelSettings.referenceResolution = portraitForceResolution;
                    _root.EnableInClassList("portrait", true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newMode), newMode, null);
            }

            mode = newMode;
        }

        private void OnDestroy()
        {
            // Revert for editor
            uiDocument.panelSettings.match = 1f;
            uiDocument.panelSettings.referenceResolution = landscapeMatchResolution;;
        }

        private enum Mode
        {
            LandscapeMatch,
            PortraitMatch,
            PortraitForce
        }
    }
}