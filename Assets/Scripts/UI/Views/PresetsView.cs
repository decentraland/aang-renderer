using System;
using System.Collections.Generic;
using Data;
using UI.Elements;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace UI.Views
{
    public class PresetsView : UIView
    {
        public event Action<ProfileResponse.Avatar.AvatarData> PresetSelected;

        private PreviewButtonElement _selectedPreset;

        private readonly List<PreviewButtonElement> _previewButtons;
        private ProfileResponse.Avatar.AvatarData[] _presets;

        public PresetsView(VisualElement root) : base(root)
        {
            _previewButtons = root.Q("Body").Query<PreviewButtonElement>().ToList();
        }

        public void SetPresets(ProfileResponse.Avatar.AvatarData[] presets, int initialSelection)
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

                // TODO: Error handling
                RemoteTextureService.Instance.RequestTexture(presetAvatar.snapshots.body,
                    tex => button.SetTexture(tex));
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