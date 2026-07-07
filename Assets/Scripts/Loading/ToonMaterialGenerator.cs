using System.Collections.Generic;
using Data;
using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using UnityEngine;
using UnityEngine.Rendering;

namespace Loading
{
    public class ToonMaterialGenerator : IMaterialGenerator
    {
        private const float EMISSIVE_MAGIC_NUMBER = 5f;

        private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int EMISSIVE_TEX_ID = Shader.PropertyToID("_Emissive_Tex");
        private static readonly int EMISSIVE_COLOR_ID = Shader.PropertyToID("_Emissive_Color");

        private static readonly int NORMAL_MAP_ID = Shader.PropertyToID("_NormalMap");
        private static readonly int NORMAL_MAP_ARR_ID = Shader.PropertyToID("_NormalMapArr_ID");
        private static readonly int BUMP_SCALE_ID = Shader.PropertyToID("_BumpScale");

        private static readonly int IS_STYLIZED_METALLIC_ID = Shader.PropertyToID("_IsStylizedMetallic");
        private static readonly int METALLIC_GLOSS_MAP_ID = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int METALLIC_GLOSS_ARR_ID = Shader.PropertyToID("_MetallicGlossMapArr_ID");

        // Cache of 1x1 masks keyed by quantized metallic value, so a uniform metallicFactor can flow
        // through the same .b mask-sampling path without allocating a texture per material. Bounded to
        // <= 256 tiny textures for the session; shared, so no per-material leak.
        private static readonly Dictionary<byte, Texture2D> UniformMetallicMasks = new();

        private static readonly int TWEAK_TRANSPARENCY_ID = Shader.PropertyToID("_Tweak_transparency");
        private static readonly int CLIPPING_LEVEL_ID = Shader.PropertyToID("_Clipping_Level");
        private static readonly int Z_WRITE_MODE_ID = Shader.PropertyToID("_ZWriteMode");
        private static readonly int SRC_BLEND_ID = Shader.PropertyToID("_SrcBlend");
        private static readonly int DST_BLEND_ID = Shader.PropertyToID("_DstBlend");
        private static readonly int CULL_MODE_ID = Shader.PropertyToID("_CullMode");

        //private readonly AvatarColors _avatarColors;

        public ToonMaterialGenerator()
        {
            
        }
        
        public ToonMaterialGenerator(AvatarColors avatarColors)
        {
            //_avatarColors = avatarColors;
        }

        public Material GenerateMaterial(int materialIndex, GLTFast.Schema.Material gltfMaterial, IGltfReadable gltf,
            bool pointsSupport = false)
        {
            var isFacialFeature = IsFacialFeature(gltfMaterial.name);
            var mat = new Material(isFacialFeature ? CommonAssets.FacialFeaturesMaterial : CommonAssets.AvatarMaterial)
                { name = gltfMaterial.name };

            // Base color and texture
            var baseColor = gltfMaterial.pbrMetallicRoughness.BaseColor;
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

            // Normal map + stylized-metallic mask are body-only features (facial features are flat).
            if (!isFacialFeature)
            {
                // Normal map — read straight from the GLB. glTFast imports textures referenced through
                // the glTF `normalTexture` slot as linear (see GltfImport.SetImageGamma), so GetTexture
                // returns a correctly-sampled normal. The DCL_Toon non-array path samples _NormalMap;
                // _NormalMapArr_ID gates it (>= 0 = enabled, -1 = fall back to the geometric normal).
                if (gltfMaterial.normalTexture != null && gltfMaterial.normalTexture.index != -1)
                {
                    mat.SetTexture(NORMAL_MAP_ID, gltf.GetTexture(gltfMaterial.normalTexture.index));
                    mat.SetInteger(NORMAL_MAP_ARR_ID, 0);
                    // NOTE: _BumpScale is a compile-time constant in DCL_ToonVariables.hlsl, so this is a
                    // no-op until that constant is promoted to a runtime property (see
                    // CHANGES_NormalMap_StylizedMetallic.md). Set anyway so it's correct once promoted.
                    mat.SetFloat(BUMP_SCALE_ID, gltfMaterial.normalTexture.scale);
                }
                else
                {
                    mat.SetInteger(NORMAL_MAP_ARR_ID, -1);
                }

                // Stylized-metallic mask — "where do we put the matcap". Driven by the GLB's PBR
                // metallic, which is how creators express it (Blender's Metallic slider = metallicFactor):
                //   - metallicRoughnessTexture  -> per-texel mask, metallic in the .b channel (glTF ORM);
                //   - else metallicFactor > 0   -> uniform amount, baked into a flat mask so it flows
                //                                  through the same .b path (honors 0..1).
                // Non-metal materials carry an explicit metallicFactor = 0, so they stay off. And since
                // the shader also needs _MatCap_SamplerArr_ID >= 0, nothing shows until a matcap is bound
                // (production leaves that unset), so existing avatars are visually unchanged regardless.
                // The matcap (the "look") is supplied separately by the debug harness / project matcap.
                var pbr = gltfMaterial.pbrMetallicRoughness;
                var hasMetalTex = pbr.metallicRoughnessTexture != null && pbr.metallicRoughnessTexture.index != -1;

                if (hasMetalTex)
                {
                    mat.SetTexture(METALLIC_GLOSS_MAP_ID, gltf.GetTexture(pbr.metallicRoughnessTexture.index));
                    mat.SetInteger(METALLIC_GLOSS_ARR_ID, 0);
                    mat.SetInteger(IS_STYLIZED_METALLIC_ID, 1);
                }
                else if (pbr.metallicFactor > 0f)
                {
                    mat.SetTexture(METALLIC_GLOSS_MAP_ID, GetUniformMetallicMask(pbr.metallicFactor));
                    mat.SetInteger(METALLIC_GLOSS_ARR_ID, 0);
                    mat.SetInteger(IS_STYLIZED_METALLIC_ID, 1);
                }
                else
                {
                    mat.SetInteger(METALLIC_GLOSS_ARR_ID, -1);
                    mat.SetInteger(IS_STYLIZED_METALLIC_ID, 0);
                }
            }

            // Alpha
            if (isFacialFeature)
            {
                mat.SetInt(Z_WRITE_MODE_ID, 0);
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (gltfMaterial.GetAlphaMode() == GLTFast.Schema.Material.AlphaMode.Blend)
            {
                mat.DisableKeyword("_IS_CLIPPING_MODE");
                mat.EnableKeyword("_IS_CLIPPING_TRANSMODE");
                mat.SetFloat(TWEAK_TRANSPARENCY_ID, 0.0f - (1.0f - baseColor.a));
                mat.SetFloat(CLIPPING_LEVEL_ID, 0);
                mat.SetInt(Z_WRITE_MODE_ID, 0);

                mat.SetFloat(SRC_BLEND_ID, (int)BlendMode.SrcAlpha);
                mat.SetFloat(DST_BLEND_ID, (int)BlendMode.OneMinusSrcAlpha);

                // I don't think we need to set this but if some transparency stuff is messed up maybe we do
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

            // Backface culling
            mat.SetInt(CULL_MODE_ID, (int)CullMode.Back);

            return mat;
        }


        // Returns a shared 1x1 linear texture whose channels all equal the (quantized) metallic value,
        // so the shader's .b mask read yields that uniform amount. Cached per value.
        private static Texture2D GetUniformMetallicMask(float metallic)
        {
            var key = (byte)Mathf.Clamp(Mathf.RoundToInt(metallic * 255f), 0, 255);
            if (UniformMetallicMasks.TryGetValue(key, out var tex) && tex != null)
                return tex;

            var v = key / 255f;
            tex = new Texture2D(1, 1, TextureFormat.RGBA32, false, true) { name = $"UniformMetallicMask_{key}" };
            tex.SetPixel(0, 0, new Color(v, v, v, 1f));
            tex.Apply(false, true);
            UniformMetallicMasks[key] = tex;
            return tex;
        }

        private static bool IsFacialFeature(string gltfMaterialName)
        {
            return gltfMaterialName is "AvatarEyebrows_MAT" or "AvatarEyes_MAT" or "AvatarMouth_MAT"
                or "AvatarMaskEyebrows_MAT" or "AvatarMaskEyes_MAT" or "AvatarMaskMouth_MAT";
        }

        // private bool TryGetColorOverride(string materialName, out Color color)
        // {
        //     if (materialName.Contains("skin", StringComparison.OrdinalIgnoreCase))
        //     {
        //         color = _avatarColors.Skin;
        //         return true;
        //     }
        //
        //     if (materialName.Contains("hair", StringComparison.OrdinalIgnoreCase))
        //     {
        //         color = _avatarColors.Hair;
        //         return true;
        //     }
        //
        //     color = default;
        //     return false;
        // }

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