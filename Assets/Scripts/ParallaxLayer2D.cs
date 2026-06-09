using UnityEngine;

/// <summary>
/// Parallax scrolling for a single background layer, on both axes.
/// <see cref="scrollSpeed"/> is the layer's apparent on-screen speed relative to the
/// camera: 0 = far (barely moves, sits at the horizon), 1 = foreground (whips past at
/// full camera speed). Internally the layer moves WITH the camera by (1 - scrollSpeed)
/// horizontally and vertically, so a high value lags the world more and reads as nearer.
/// For seamless infinite horizontal scroll, use a SpriteRenderer in Tiled draw mode
/// several tiles wide; this script snaps the layer back by whole tile widths so the
/// repeat is never visible.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer2D : MonoBehaviour
{
    [Header("Parallax")]
    [Tooltip("Apparent on-screen scroll speed. 0 = far/slow (horizon), 1 = near/fast (foreground).")]
    [SerializeField, Range(0f, 1f)] private float scrollSpeed = 0.5f;

    [Tooltip("Vertical parallax strength as a fraction of horizontal. 0 = pinned vertically (no drift), 1 = equal to horizontal.")]
    [SerializeField, Range(0f, 1f)] private float verticalParallaxScale = 1f;

    [Tooltip("Snap back by whole tile widths so a Tiled layer scrolls infinitely with no seam.")]
    [SerializeField] private bool infiniteHorizontal = true;

    private Transform cam;
    private Vector3 previousCamPosition;
    private float tileWorldWidth;

    private void Start()
    {
        Camera main = Camera.main;
        if (main != null)
        {
            cam = main.transform;
            previousCamPosition = cam.position;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            // Width of a single sprite tile in world units (independent of Tiled size).
            tileWorldWidth = sr.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x);
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            return;
        }

        Vector3 delta = cam.position - previousCamPosition;

        // Follow the camera by (1 - scrollSpeed): far layers (low speed) track the camera
        // closely and barely appear to move; near layers (high speed) lag the world and
        // sweep past quickly. Vertical parallax is dampened by verticalParallaxScale so the
        // layers drift less vertically and stay within their (taller) coverage.
        float followX = 1f - scrollSpeed;
        float followY = 1f - scrollSpeed * verticalParallaxScale;
        transform.position += new Vector3(delta.x * followX, delta.y * followY, 0f);
        previousCamPosition = cam.position;

        if (infiniteHorizontal && tileWorldWidth > 0f)
        {
            float dx = cam.position.x - transform.position.x;
            if (Mathf.Abs(dx) >= tileWorldWidth)
            {
                // Snap by whole tiles toward the camera; invisible because the layer tiles.
                float snap = tileWorldWidth * Mathf.Round(dx / tileWorldWidth);
                transform.position += new Vector3(snap, 0f, 0f);
            }
        }
    }
}
