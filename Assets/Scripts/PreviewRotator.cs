using UnityEngine;

/// <summary>
/// Handles autorotation and inertia-based user rotation.
/// </summary>
public class PreviewRotator : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    [Header("Drag Settings")] [SerializeField]
    private float dragSpeed = 0.2f;

    [SerializeField] private float inertiaDamp = 0.95f;

    [Header("Auto-Rotate Settings")] [SerializeField]
    private float autoRotateSpeed = 20f;

    [SerializeField] private float autoRotateDelay = 2f;
    [SerializeField] private float returnSpeed = 2f;

    private float _horizontalVel;
    private float _verticalVel;
    private float _lastDragTime;
    private Quaternion _initialRotation;

    public bool AllowVertical { get; set; } = true;
    public bool EnableAutoRotate { get; set; } = true;

    private void Awake()
    {
        _initialRotation = transform.rotation;

        // Web builds have an issue where the sensitivity of mouse is way too high, so we dampen it.
        if (!Application.isEditor)
        {
            dragSpeed *= 0.05f;
        }
    }

    private void Update()
    {
        var dt = Time.deltaTime;

        if (Input.GetMouseButton(0))
        {
            var mouseDelta = Input.mousePositionDelta;
            _horizontalVel += -mouseDelta.x * dragSpeed;
            _verticalVel += mouseDelta.y * dragSpeed;
            _lastDragTime = Time.time;
        }

        // Framerate-independent dampening
        _horizontalVel *= Mathf.Pow(inertiaDamp, dt);
        _verticalVel *= Mathf.Pow(inertiaDamp, dt);

        // Velocity rotation
        transform.Rotate(Vector3.up, _horizontalVel, Space.World);
        if (AllowVertical) transform.Rotate(Vector3.right, _verticalVel, Space.World);

        // Auto rotation
        if (Time.time - _lastDragTime > autoRotateDelay)
        {
            var euler = transform.eulerAngles;
            euler.x = Mathf.LerpAngle(euler.x, 0f, returnSpeed * dt);
            euler.z = Mathf.LerpAngle(euler.z, 0f, returnSpeed * dt);
            transform.eulerAngles = euler;

            if (EnableAutoRotate) transform.Rotate(Vector3.up, autoRotateSpeed * dt, Space.World);
        }
    }

    public void ResetRotation()
    {
        transform.rotation = _initialRotation;
        _horizontalVel = 0;
        _verticalVel = 0;
        _lastDragTime = 0;
    }
}