using System;
using Data;
using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using UnityEngine;

namespace GLTF
{
    public class AvatarMaterialGenerator : IMaterialGenerator
    {
        private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private readonly AvatarColors _avatarColors;

        private Texture2DArray _texture2DArray;

        public AvatarMaterialGenerator(AvatarColors avatarColors)
        {
            _avatarColors = avatarColors;
        }

        public Material GenerateMaterial(int materialIndex, GLTFast.Schema.Material gltfMaterial, IGltfReadable gltf,
            bool pointsSupport = false)
        {
            var mat = new Material(CommonAssets.AvatarMaterial) { name = gltfMaterial.name };

            if (TryGetColorOverride(gltfMaterial.name, out var color))
            {
                mat.SetColor(BaseColorID, color);
            }

            mat.SetTexture(MainTexID, gltf.GetTexture(materialIndex));

            return mat;
        }

        // TODO: This is probably wrong
        private bool TryGetColorOverride(string materialName, out Color color)
        {
            if (materialName.Contains("skin", StringComparison.OrdinalIgnoreCase) ||
                materialName.Contains("mouth", StringComparison.OrdinalIgnoreCase))
            {
                color = _avatarColors.Skin;
                return true;
            }

            if (materialName.Contains("hair", StringComparison.OrdinalIgnoreCase) ||
                materialName.Contains("eyebrows", StringComparison.OrdinalIgnoreCase))
            {
                color = _avatarColors.Hair;
                return true;
            }

            if (materialName.Contains("eyes", StringComparison.OrdinalIgnoreCase))
            {
                color = _avatarColors.Eyes;
                return true;
            }

            color = default;
            return false;
        }

        public Material GetDefaultMaterial(bool pointsSupport = false)
        {
            // I don't think this is ever called
            return CommonAssets.AvatarMaterial;
        }

        public void SetLogger(ICodeLogger logger)
        {
            // We don't need a logger
        }
    }
}