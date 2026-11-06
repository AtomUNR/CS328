using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private SpriteRenderer mapBackground;

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

    private void Start()
    {
        SnapToTarget();
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

        transform.position = ClampToMap(cameraPosition);
    }

    public void SnapToTarget()
    {
        if (followCamera == null)
            followCamera = GetComponent<Camera>();

        if (target == null || !followCamera.orthographic)
            return;

        Vector3 cameraPosition = transform.position;
        cameraPosition.x = target.position.x;
        cameraPosition.y = target.position.y;

        transform.position = ClampToMap(cameraPosition);
    }

    // Use this when transitioning to another area later.
    public void SetMapBackground(SpriteRenderer newBackground)
    {
        mapBackground = newBackground;
        SnapToTarget();
    }

    private Vector3 ClampToMap(Vector3 position)
    {
        if (mapBackground == null || mapBackground.sprite == null)
            return position;

        Bounds bounds = mapBackground.bounds;

        float halfHeight = followCamera.orthographicSize;
        float halfWidth = halfHeight * followCamera.aspect;

        // If the map is narrower than the view, center horizontally.
        if (bounds.size.x <= halfWidth * 2f)
        {
            position.x = bounds.center.x;
        }
        else
        {
            position.x = Mathf.Clamp(
                position.x,
                bounds.min.x + halfWidth,
                bounds.max.x - halfWidth
            );
        }

        // If the map is shorter than the view, center vertically.
        if (bounds.size.y <= halfHeight * 2f)
        {
            position.y = bounds.center.y;
        }
        else
        {
            position.y = Mathf.Clamp(
                position.y,
                bounds.min.y + halfHeight,
                bounds.max.y - halfHeight
            );
        }

        return position;
    }
}