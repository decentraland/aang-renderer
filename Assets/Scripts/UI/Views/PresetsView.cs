using System;
using System.Collections.Generic;
using Services;
using UI.Elements;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class PresetsView : StageView
    {
        public event Action<PresetDefinition> PresetSelected;

        private PreviewButtonElement _selectedPreset;

        private readonly List<PreviewButtonElement> _previewButtons;
        private PresetDefinition[] _presets;

        public override string SelectedCategory => null;
        
        public override object GetData()
        {
            return (_presets, _selectedPreset == null ? -1 : _selectedPreset.parent.IndexOf(_selectedPreset));
        }

        public override void SetData(object data)
        {
            var cast = ((PresetDefinition[] presets, int selectedIndex)) data;
            
            SetPresets(cast.presets, cast.selectedIndex);
        }

        public PresetsView(VisualElement root, string title, string confirmButtonText, int confirmButtonWidth,
            string confirmButtonTextMobile, bool canSkip) : base(root, title, confirmButtonText, confirmButtonWidth,
            confirmButtonTextMobile, canSkip)
        {
            _previewButtons = root.Q("Body").Query<PreviewButtonElement>().ToList();
        }

        public void SetPresets(PresetDefinition[] presets, int initialSelection)
        {
            Assert.AreEqual(presets.Length, _previewButtons.Count);

            _presets = presets;

            for (var i = 0; i < presets.Length; i++)
            {
                var index = i;
                var presetAvatar = presets[i];
                var button = _previewButtons[i];

                button.Clicked += () => OnPresetClicked(index);

                if (i == initialSelection)
                {
                    button.Selected = true;
                    _selectedPreset = button;
                }

                // TODO: Error handling?
                RemoteTextureService.Instance.RequestTexture(presetAvatar.thumbnail, tex => button.SetTexture(tex));
            }
        }

        public void ClearSelection()
        {
            if (_selectedPreset == null) return;

            _selectedPreset.Selected = false;
            _selectedPreset = null;
        }

        private void OnPresetClicked(int index)
        {
            RefreshSelection(index);

            PresetSelected!(_presets[index]);
        }

        private void RefreshSelection(int index)
        {
            if (_selectedPreset != null) _selectedPreset.Selected = false;
            _selectedPreset = _previewButtons[index];
            _selectedPreset.Selected = true;
        }
    }
}