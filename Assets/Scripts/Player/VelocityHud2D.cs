using UnityEngine;

// Tiny debug overlay: prints the player's current velocity in the top-right corner while tuning
// movement / recoil. Uses OnGUI so there's no Canvas/TMP wiring — drop it on the Player (or any
// object near the Rigidbody2D) and it auto-resolves the body. Toggle off (or strip the component)
// before shipping.
public class VelocityHud2D : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Rigidbody to read. Auto-resolves from this object / its parents if left empty.")]
    [SerializeField] private Rigidbody2D body;

    [Header("Display")]
    [Tooltip("Master on/off so the readout can stay in the scene but hidden.")]
    [SerializeField] private bool show = true;
    [Tooltip("Pixels inset from the top-right corner of the screen.")]
    [SerializeField] private Vector2 margin = new Vector2(12f, 12f);
    [SerializeField] private int fontSize = 16;
    [SerializeField] private Color textColor = Color.white;

    private GUIStyle style;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponentInParent<Rigidbody2D>();
        }
    }

    private void OnGUI()
    {
        if (!show || body == null)
        {
            return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
            };
        }
        style.normal.textColor = textColor;

        Vector2 v = body.linearVelocity;
        string text =
            $"vel  x {v.x,7:0.00}  y {v.y,7:0.00}\n" +
            $"speed {v.magnitude,7:0.00}\n" +
            $"dir   {Compass(v),7}";

        // Right-aligned block anchored to the top-right corner.
        const float width = 260f;
        const float height = 70f;
        Rect rect = new Rect(
            Screen.width - width - margin.x,
            margin.y,
            width,
            height
        );
        GUI.Label(rect, text, style);
    }

    // 8-direction compass label for the velocity vector (matches the aim quantization).
    // "--" when essentially still.
    private static string Compass(Vector2 v)
    {
        if (v.sqrMagnitude < 0.0001f)
        {
            return "--";
        }

        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        int octant = Mathf.RoundToInt((angle + 360f) / 45f) % 8;
        string[] names = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };
        return names[octant];
    }
}
