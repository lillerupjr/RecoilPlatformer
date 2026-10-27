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

    [Header("Shake")]
    [Tooltip("Max positional shake (world units) at full trauma.")]
    [SerializeField] private float maxShakeOffset = 0.5f;
    [Tooltip("Max camera roll (degrees) at full trauma. 0 = positional shake only "
        + "(safer for pixel art / parallax).")]
    [SerializeField] private float maxShakeAngle = 0f;
    [Tooltip("How fast the shake wobbles. Higher = more frantic.")]
    [SerializeField] private float shakeFrequency = 25f;
    [Tooltip("Trauma drained per second. Higher = shorter shakes.")]
    [SerializeField] private float traumaDecay = 1.5f;

    private Vector3 velocity;
    private float trauma;
    private float shakeSeed;

    // Adds shake energy (0-1, clamped). Accumulates so rapid hits stack and cap at full trauma.
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    private void Awake()
    {
        // Randomize the noise sample line so two cameras (or restarts) don't shake identically.
        shakeSeed = Random.value * 100f;
    }

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

        Vector3 followPos = Vector3.SmoothDamp(
            current,
            desired,
            ref velocity,
            smoothTime
        );

        ApplyShake(followPos);
    }

    // Layers shake on top of the follow position so it never fights SmoothDamp. Uses trauma^2 (the
    // classic Squirrel Eiserloh curve) so small hits stay subtle and big ones punch. Sampled on
    // unscaled time so the camera keeps shaking through a hit-stop freeze (Time.timeScale = 0).
    private void ApplyShake(Vector3 followPos)
    {
        if (trauma <= 0f)
        {
            transform.position = followPos;
            if (transform.rotation != Quaternion.identity)
            {
                transform.rotation = Quaternion.identity;
            }
            return;
        }

        float shake = trauma * trauma;
        float t = Time.unscaledTime * shakeFrequency;

        // Perlin noise (centered to [-1, 1]) is smoother than pure random — less harsh jitter.
        float offsetX = (Mathf.PerlinNoise(shakeSeed, t) * 2f - 1f) * maxShakeOffset * shake;
        float offsetY = (Mathf.PerlinNoise(shakeSeed + 17f, t) * 2f - 1f) * maxShakeOffset * shake;
        float angle = (Mathf.PerlinNoise(shakeSeed + 31f, t) * 2f - 1f) * maxShakeAngle * shake;

        transform.position = followPos + new Vector3(offsetX, offsetY, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.unscaledDeltaTime);
    }
}
