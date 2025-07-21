using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Rendering
{
    public class BackgroundRenderPass : ScriptableRenderPass
    {
        private const string k_BlurTextureName = "_BlurTexture";
        private const string k_VerticalPassName = "VerticalBlurRenderPass";
        private const string k_HorizontalPassName = "HorizontalBlurRenderPass";

        private readonly Material _material;

        public BackgroundRenderPass(Material material)
        {
            _material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer) return;

            var colorTarget = resourceData.activeColorTexture;

            if (!colorTarget.IsValid())
                return;

            var drawParams = new RenderGraphUtils.BlitMaterialParameters(
                TextureHandle.nullHandle,
                colorTarget,
                _material,
                0 // Shader pass index
            );

            renderGraph.AddBlitPass(drawParams, "GradientPass");

            // // Depth mask pass
            // var maskPassParams = new RenderGraphUtils.BlitMaterialParameters(
            //     TextureHandle.nullHandle,
            //     colorTarget,
            //     _maskMaterial,
            //     0
            // );
            // renderGraph.AddBlitPass(maskPassParams, "DepthMaskPass");
            //
            //
            // var blurTextureDescriptor = resourceData.activeColorTexture.GetDescriptor(renderGraph);
            // blurTextureDescriptor.name = k_BlurTextureName;
            // blurTextureDescriptor.depthBufferBits = 0;
            // var dst = renderGraph.CreateTexture(blurTextureDescriptor);
            //
            // // The AddBlitPass method adds a vertical blur render graph pass that blits from the source texture (camera color in this case) to the destination texture using the first shader pass (the shader pass is defined in the last parameter).
            // RenderGraphUtils.BlitMaterialParameters paraVertical = new(colorTarget, dst, _blurMaterial, 0);
            // renderGraph.AddBlitPass(paraVertical, k_VerticalPassName);
            //
            // // The AddBlitPass method adds a horizontal blur render graph pass that blits from the texture written by the vertical blur pass to the camera color texture. The method uses the second shader pass.
            // RenderGraphUtils.BlitMaterialParameters paraHorizontal = new(dst, colorTarget, _blurMaterial, 1);
            // renderGraph.AddBlitPass(paraHorizontal, k_HorizontalPassName);
        }
    }
}