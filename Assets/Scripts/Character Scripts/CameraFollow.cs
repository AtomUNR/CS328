using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Distance from screen center before following")]
    [SerializeField, Range(0f, 0.45f)]
    private float horizontalDeadZone = 0.3f;

    [SerializeField, Range(0f, 0.45f)]
    private float verticalDeadZone = 0.3f;

    private Camera followCamera;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null || !followCamera.orthographic)
            return;

        float viewHeight = followCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * followCamera.aspect;

        float horizontalLimit = viewWidth * horizontalDeadZone;
        float verticalLimit = viewHeight * verticalDeadZone;

        Vector3 cameraPosition = transform.position;
        Vector3 difference = target.position - cameraPosition;

        if (Mathf.Abs(difference.x) > horizontalLimit)
        {
            cameraPosition.x = target.position.x
                - Mathf.Sign(difference.x) * horizontalLimit;
        }

        if (Mathf.Abs(difference.y) > verticalLimit)
        {
            cameraPosition.y = target.position.y
                - Mathf.Sign(difference.y) * verticalLimit;
        }

        // Preserve the camera's Z position.
        transform.position = cameraPosition;
    }
}