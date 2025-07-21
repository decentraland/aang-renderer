using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rendering
{
    public class BackgroundRendererFeature : ScriptableRendererFeature
    {
        private static readonly int INNER_COLOR_ID = Shader.PropertyToID("_InnerColor");
        private static readonly int OUTER_COLOR_ID = Shader.PropertyToID("_OuterColor");

        [SerializeField] private BackgroundSettings backgroundSettings;
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent renderPassEvent;

        private Material material;
        private BackgroundRenderPass renderPass;

        public override void Create()
        {
            if (shader == null)
            {
                return;
            }

            material = new Material(shader);
            material.SetColor(INNER_COLOR_ID, backgroundSettings.center);
            material.SetColor(OUTER_COLOR_ID, backgroundSettings.outer);

            renderPass = new BackgroundRenderPass(material)
            {
                renderPassEvent = renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass == null)
            {
                return;
            }

            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(renderPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }

    [Serializable]
    public class BackgroundSettings
    {
        public Color center;
        public Color outer;
    }
}