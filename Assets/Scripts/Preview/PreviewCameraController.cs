using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Preview
{
    public class PreviewCameraController : MonoBehaviour
    {
        [SerializeField] private float minFOV = 10f;
        [SerializeField] private float maxFOV = 30f;
        [SerializeField] private float zoomStep = 5f;
        [SerializeField] private float lerpSpeed = 1f;

        [SerializeField] private CinemachineCamera authProfileCamera;
        [SerializeField] private CinemachineCamera marketplaceCamera;
        [SerializeField] private CinemachineCamera builderCamera;

        private float _targetFOV;
        private float _initialFOV;

        private void Awake()
        {
            _targetFOV = _initialFOV = marketplaceCamera.Lens.FieldOfView;
        }

        public void SetMode(PreviewMode mode)
        {
            // Reset FOV when switching modes
            marketplaceCamera.Lens.FieldOfView = _targetFOV = _initialFOV;
            
            switch (mode)
            {
                case PreviewMode.Marketplace:
                    marketplaceCamera.Prioritize();
                    break;
                case PreviewMode.Authentication:
                case PreviewMode.Profile:
                    authProfileCamera.Prioritize();
                    break;
                case PreviewMode.Builder:
                    builderCamera.Prioritize();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void Update()
        {
            marketplaceCamera.Lens.FieldOfView = Mathf.Lerp(marketplaceCamera.Lens.FieldOfView, _targetFOV, Time.deltaTime * lerpSpeed);
        }

        public void ZoomIn()
        {
            _targetFOV = Mathf.Clamp(_targetFOV - zoomStep, minFOV, maxFOV);
        }

        public void ZoomOut()
        {
            _targetFOV = Mathf.Clamp(_targetFOV + zoomStep, minFOV, maxFOV);
        }
    }
}