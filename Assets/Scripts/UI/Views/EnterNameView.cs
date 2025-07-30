using System;
using UI.Elements;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class EnterNameView
    {
        public event Action<string, string> Confirmed;

        private readonly Toggle _tosToggle;
        private readonly TextField _usernameField;
        private readonly TextField _emailField;
        private readonly DCLButtonElement _confirmButton;

        public EnterNameView(VisualElement root)
        {
            _confirmButton = root.Q<DCLButtonElement>("CustomizeButton");
            _confirmButton.Clicked += OnCustomizeClicked;

            _tosToggle = root.Q<Toggle>("TOSToggle");
            _usernameField = root.Q<TextField>("UsernameField");
            _emailField = root.Q<TextField>("EmailField");

            _tosToggle.RegisterValueChangedCallback(_ => RefreshButton());
            _usernameField.RegisterValueChangedCallback(_ => RefreshButton());
        }

        private void RefreshButton()
        {
            _confirmButton.SetEnabled(_tosToggle.value && IsUsernameValid());
        }

        private bool IsUsernameValid()
        {
            return !string.IsNullOrWhiteSpace(_usernameField.value);
        }

        private void OnCustomizeClicked()
        {
            Confirmed!(_usernameField.value, _emailField.value);
        }
    }
}