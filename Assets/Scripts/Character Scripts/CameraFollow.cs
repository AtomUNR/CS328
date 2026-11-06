using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private SpriteRenderer mapBackground;
    [Header("Distance from screen center before following")]
    [SerializeField, Range(0f, 0.45f)] private float horizontalDeadZone = 0.3f;
    [SerializeField, Range(0f, 0.45f)] private float verticalDeadZone = 0.3f;
    private Camera followCamera;

    private void Awake() { followCamera = GetComponent<Camera>(); }
    private void Start() { SnapToTarget(); }

    public void SetTarget(Transform newTarget) { target = newTarget; }

    public void SetMapBackground(SpriteRenderer newBackground)
    {
        mapBackground = newBackground;
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (followCamera == null) followCamera = GetComponent<Camera>();
        if (target == null || !followCamera.orthographic) return;
        transform.position = ClampToMap(new Vector3(target.position.x, target.position.y, transform.position.z));
    }

    private void LateUpdate()
    {
        if (target == null || !followCamera.orthographic) return;
        float height = followCamera.orthographicSize * 2f;
        float horizontalLimit = height * followCamera.aspect * horizontalDeadZone;
        float verticalLimit = height * verticalDeadZone;
        Vector3 position = transform.position;
        Vector3 difference = target.position - position;
        if (Mathf.Abs(difference.x) > horizontalLimit)
            position.x = target.position.x - Mathf.Sign(difference.x) * horizontalLimit;
        if (Mathf.Abs(difference.y) > verticalLimit)
            position.y = target.position.y - Mathf.Sign(difference.y) * verticalLimit;
        transform.position = ClampToMap(position);
    }

    private Vector3 ClampToMap(Vector3 position)
    {
        if (mapBackground == null || mapBackground.sprite == null) return position;
        Bounds bounds = mapBackground.bounds;
        float halfHeight = followCamera.orthographicSize;
        float halfWidth = halfHeight * followCamera.aspect;
        position.x = bounds.size.x <= halfWidth * 2f ? bounds.center.x :
            Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        position.y = bounds.size.y <= halfHeight * 2f ? bounds.center.y :
            Mathf.Clamp(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
        return position;
    }
}
