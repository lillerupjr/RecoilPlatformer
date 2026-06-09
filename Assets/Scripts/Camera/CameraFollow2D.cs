using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = new Vector2(0f, 1f);

    [Header("Follow")]
    [Tooltip("Approximate time to reach the target. Lower = snappier.")]
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 current = transform.position;
        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            current.z
        );

        // Respect locked axes so the camera can be pinned vertically/horizontally.
        if (!followX)
        {
            desired.x = current.x;
        }

        if (!followY)
        {
            desired.y = current.y;
        }

        transform.position = Vector3.SmoothDamp(
            current,
            desired,
            ref velocity,
            smoothTime
        );
    }
}
