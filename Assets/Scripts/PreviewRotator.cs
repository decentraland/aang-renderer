using UnityEngine;

public class PreviewRotator : MonoBehaviour
{
    [SerializeField] private Camera previewCamera;
    [SerializeField] private float autoRotationSpeed = 30f;
    [SerializeField] private float dragSensitivity = 0.2f;
    [SerializeField] private float rotationResumeDelay = 1f;
    [SerializeField] private float resetSpeed = 2f;

    private bool _isDragging;
    private float _lastDragTime;
    private Vector3 _previousMousePos;
    private Vector3 _combinedCenter;

    /// <summary>
    /// Recalculate the combined center for all child SkinnedMeshRenderers.
    /// Call this if the meshes change at runtime.
    /// </summary>
    public void RecalculateBounds()
    {
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(false);
        if (renderers.Length > 0)
        {
            var bounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            _combinedCenter = bounds.center;
        }
        else
        {
            _combinedCenter = transform.position;
        }
    }

    private void Update()
    {
        var e = transform.eulerAngles;
        e.y += autoRotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Euler(e);

        return;

        HandleDrag();
        if (!_isDragging)
        {
            // After dragging stops, wait a bit, then try to reset X/Z to 0 for upright
            var shouldReset = (Time.time - _lastDragTime) > rotationResumeDelay;

            // Get current rotation in euler
            var euler = transform.eulerAngles;

            // Smoothly zero out X and Z if we need to reset
            if (shouldReset)
            {
                euler.x = Mathf.LerpAngle(euler.x, 0f, resetSpeed * Time.deltaTime);
                euler.z = Mathf.LerpAngle(euler.z, 0f, resetSpeed * Time.deltaTime);
            }

            // Always auto-rotate around Y
            euler.y += autoRotationSpeed * Time.deltaTime;

            // Apply final rotation around the same pivot
            PivotSetEuler(euler);
        }
    }

    private void HandleDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = true;
            _previousMousePos = Input.mousePosition;
        }

        if (_isDragging && Input.GetMouseButton(0))
        {
            var delta = Input.mousePosition - _previousMousePos;

            // Drag rotation
            var rotX = delta.y * dragSensitivity;
            var rotY = -delta.x * dragSensitivity;

            transform.RotateAround(_combinedCenter, previewCamera.transform.right, rotX);
            transform.RotateAround(_combinedCenter, Vector3.up, rotY);

            _previousMousePos = Input.mousePosition;
            _lastDragTime = Time.time;
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
            _lastDragTime = Time.time;
        }
    }

    /// <summary>
    /// Sets the transform's Euler angles around a pivot so it doesn't drift.
    /// </summary>
    private void PivotSetEuler(Vector3 newEuler)
    {
        // Calculate current offset from pivot
        var offset = transform.position - _combinedCenter;
        var oldRot = transform.rotation;

        // Build the new rotation
        var newRot = Quaternion.Euler(newEuler);

        // Figure out how much the object has changed rotation-wise
        var relative = newRot * Quaternion.Inverse(oldRot);

        // Rotate our offset by that same delta
        offset = relative * offset;

        // Update position & rotation
        transform.position = _combinedCenter + offset;
        transform.rotation = newRot;
    }
}