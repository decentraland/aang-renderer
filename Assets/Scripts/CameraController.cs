using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float minFOV = 40f;
    [SerializeField] private float maxFOV = 100f;
    [SerializeField] private float lerpSpeed = 1f;

    [SerializeField] private Vector3 marketplacePosition;
    [SerializeField] private Vector3 authPosition;
    [SerializeField] private Vector3 profilePosition;
    [SerializeField] private Vector3 builderPosition;

    private float _targetFOV;
    private float _initialFOV;

    private void Awake()
    {
        _targetFOV = _initialFOV = mainCamera.fieldOfView;
    }

    public void SetMode(PreviewMode mode)
    {
        // Reset FOV when switching modes
        mainCamera.fieldOfView = _targetFOV = _initialFOV;
        
        transform.position = mode switch
        {
            PreviewMode.Marketplace => marketplacePosition,
            PreviewMode.Authentication => authPosition,
            PreviewMode.Profile => profilePosition,
            PreviewMode.Builder => builderPosition,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private void Update()
    {
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, _targetFOV, Time.deltaTime * lerpSpeed);
    }

    public void ZoomIn()
    {
        _targetFOV = Mathf.Clamp(_targetFOV - 10, minFOV, maxFOV);
    }

    public void ZoomOut()
    {
        _targetFOV = Mathf.Clamp(_targetFOV + 10, minFOV, maxFOV);
    }
}