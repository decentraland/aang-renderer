using System;
using Data;
using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using UnityEngine;
using UnityEngine.Rendering;

namespace GLTF
{
    public class ToonMaterialGenerator : IMaterialGenerator
    {
        private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int EMISSIVE_TEX_ID = Shader.PropertyToID("_Emissive_Tex");
        private static readonly int EMISSIVE_COLOR_ID = Shader.PropertyToID("_Emissive_Color");

        private static readonly int TWEAK_TRANSPARENCY_ID = Shader.PropertyToID("_Tweak_transparency");
        private static readonly int CLIPPING_LEVEL_ID = Shader.PropertyToID("_Clipping_Level");
        private static readonly int Z_WRITE_MODE_ID = Shader.PropertyToID("_ZWriteMode");

        private readonly AvatarColors _avatarColors;

        private readonly float EMISSIVE_MAGIC_NUMBER = 5f;

        public ToonMaterialGenerator(AvatarColors avatarColors)
        {
            _avatarColors = avatarColors;
        }

        public Material GenerateMaterial(int materialIndex, GLTFast.Schema.Material gltfMaterial, IGltfReadable gltf,
            bool pointsSupport = false)
        {
            var mat = new Material(GetMaterial(gltfMaterial.name)) { name = gltfMaterial.name };

            // Base color and texture
            var baseColor = TryGetColorOverride(gltfMaterial.name, out var color)
                ? color
                : gltfMaterial.pbrMetallicRoughness.BaseColor;
            mat.SetColor(BASE_COLOR_ID, baseColor);

            if (gltfMaterial.pbrMetallicRoughness.baseColorTexture.index != -1)
            {
                mat.SetTexture(MAIN_TEX_ID, gltf.GetTexture(gltfMaterial.pbrMetallicRoughness.baseColorTexture.index));
            }

            // Emission
            mat.SetColor(EMISSIVE_COLOR_ID, gltfMaterial.Emissive * EMISSIVE_MAGIC_NUMBER);
            if (gltfMaterial.emissiveTexture.index != -1)
            {
                mat.SetTexture(EMISSIVE_TEX_ID, gltf.GetTexture(gltfMaterial.emissiveTexture.index));
            }

            // Alpha
            if (gltfMaterial.GetAlphaMode() == GLTFast.Schema.Material.AlphaMode.Blend)
            {
                mat.DisableKeyword("_IS_CLIPPING_MODE");
                mat.EnableKeyword("_IS_CLIPPING_TRANSMODE");
                mat.SetFloat(TWEAK_TRANSPARENCY_ID, 0.0f - (1.0f - baseColor.a));
                mat.SetFloat(CLIPPING_LEVEL_ID, gltfMaterial.alphaCutoff);
                // TODO Do we need this?
                // mat.SetInt(Z_WRITE_MODE_ID, (int)originalMaterial.GetFloat(Z_WRITE));
                // mat.SetFloat(SRC_BLEND, originalMaterial.GetFloat(SRC_BLEND));
                // mat.SetFloat(DST_BLEND, originalMaterial.GetFloat(DST_BLEND));
                // mat.SetFloat(ALPHA_SRC_BLEND_TARGET, originalMaterial.GetFloat(ALPHA_SRC_BLEND_ORIGINAL));
                // mat.SetFloat(ALPHA_DST_BLEND_TARGET, originalMaterial.GetFloat(ALPHA_DST_BLEND_ORIGINAL));
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else if (gltfMaterial.GetAlphaMode() == GLTFast.Schema.Material.AlphaMode.Mask)
            {
                mat.EnableKeyword("_IS_CLIPPING_MODE");
                mat.DisableKeyword("_IS_CLIPPING_TRANSMODE");
                mat.SetFloat(TWEAK_TRANSPARENCY_ID, 0.0f - (1.0f - baseColor.a));
                mat.SetFloat(CLIPPING_LEVEL_ID, gltfMaterial.alphaCutoff);
                mat.SetInt(Z_WRITE_MODE_ID, 1);
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }

            return mat;
        }

        private Material GetMaterial(string gltfMaterialName)
        {
            return gltfMaterialName is "AvatarEyebrows_MAT" or "AvatarEyes_MAT" or "AvatarMouth_MAT"
                or "AvatarMaskEyebrows_MAT" or "AvatarMaskEyes_MAT" or "AvatarMaskMouth_MAT"
                ? CommonAssets.FacialFeaturesMaterial
                : CommonAssets.AvatarMaterial;
        }

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