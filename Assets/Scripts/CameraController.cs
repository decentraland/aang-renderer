using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float minFOV = 40f;
    [SerializeField] private float maxFOV = 100f;
    [SerializeField] private float lerpSpeed = 1f;

    private float _targetFOV;

    private void Awake()
    {
        _targetFOV = mainCamera.fieldOfView;
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