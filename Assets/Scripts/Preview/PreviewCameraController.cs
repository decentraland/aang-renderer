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

        [Tooltip("Extra breathing room around the emote when framing (0.15 = 15% margin).")]
        [SerializeField] private float framingPadding = 0.15f;

        [Tooltip("Closest the avatar camera may dolly when framing, to avoid clipping into the mesh.")]
        [SerializeField] private float minFramingDistance = 1f;

        [SerializeField] private CinemachineCamera authProfileCamera;
        [SerializeField] private CinemachineCamera marketplaceWearableCamera;
        [SerializeField] private CinemachineCamera marketplaceAvatarCamera;
        [SerializeField] private CinemachineCamera builderCamera;
        [SerializeField] private CinemachineCamera jesusCamera;

        private float _targetFOV;
        private float _initialFOV;
        private float _initialOrthoSize;
        private Vector3 _avatarCamInitialLocalPos;

        private void Awake()
        {
            _targetFOV = _initialFOV = marketplaceAvatarCamera.Lens.FieldOfView;
            _initialOrthoSize = marketplaceAvatarCamera.Lens.OrthographicSize;
            _avatarCamInitialLocalPos = marketplaceAvatarCamera.transform.localPosition;

            // We prioritize this one because we want to have a cut to any other camera after this for the first time
            authProfileCamera.Prioritize();
        }

        /// <summary>
        /// Frames the avatar camera so the given world-space emote bounds fit fully in view.
        /// Framing only moves the camera (dolly + recenter); field of view is left to the zoom
        /// controls so the two never fight. Reset back to the authored pose in <see cref="SetMode"/>.
        /// </summary>
        public void FrameAvatarToBounds(Bounds worldBounds, float aspect, bool orthographic)
        {
            var extents = worldBounds.extents;

            if (orthographic)
            {
                // Distance is irrelevant in orthographic; size the lens instead.
                var halfHeight = Mathf.Max(extents.y, extents.x / Mathf.Max(aspect, 0.0001f));
                marketplaceAvatarCamera.Lens.OrthographicSize = halfHeight * (1f + framingPadding);
                return;
            }

            // Fit distance using the tighter of the vertical/horizontal frustum so nothing clips,
            // then add half the depth so the far face stays inside, then padding for breathing room.
            var tanV = Mathf.Tan(_initialFOV * 0.5f * Mathf.Deg2Rad);
            var distV = extents.y / tanV;
            var distH = extents.x / (tanV * Mathf.Max(aspect, 0.0001f));
            var dist = (Mathf.Max(distV, distH) + extents.z) * (1f + framingPadding);
            dist = Mathf.Clamp(dist, minFramingDistance, marketplaceAvatarCamera.Lens.FarClipPlane);

            var camTransform = marketplaceAvatarCamera.transform;

            // Place the camera on its own (fixed-orientation) view axis, `dist` behind the bbox
            // center. This guarantees the bbox center projects to screen center regardless of the
            // camera's authored pitch — nudging the authored local position instead would mis-aim a
            // tilted camera and push the subject out of frame.
            var forward = camTransform.rotation * Vector3.forward;
            camTransform.position = worldBounds.center - forward * dist;
        }

        public void SetMode(PreviewMode mode)
        {
            // Reset FOV when switching modes
            marketplaceAvatarCamera.Lens.FieldOfView = marketplaceWearableCamera.Lens.FieldOfView =
                builderCamera.Lens.FieldOfView = _targetFOV = _initialFOV;

            // Reset any per-emote framing so repeated reloads always start from the authored pose.
            marketplaceAvatarCamera.transform.localPosition = _avatarCamInitialLocalPos;
            marketplaceAvatarCamera.Lens.OrthographicSize = _initialOrthoSize;

            switch (mode)
            {
                // Marketplace goes to authProfile too since we want the first blend to be a cut
                case PreviewMode.Marketplace:
                case PreviewMode.Authentication:
                case PreviewMode.Profile:
                    authProfileCamera.Prioritize();
                    break;
                case PreviewMode.Jesus:
                    jesusCamera.Prioritize();
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
            var fov = Mathf.Lerp(marketplaceAvatarCamera.Lens.FieldOfView, _targetFOV, Time.deltaTime * lerpSpeed);
            marketplaceAvatarCamera.Lens.FieldOfView = marketplaceWearableCamera.Lens.FieldOfView =
                builderCamera.Lens.FieldOfView = fov;
        }

        public void ShowMarketplaceWearable(bool showWearable)
        {
            if (showWearable)
            {
                marketplaceWearableCamera.Prioritize();
            }
            else
            {
                marketplaceAvatarCamera.Prioritize();
            }
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