using UnityEngine;
using UnityEngine.Tilemaps;

// A tile that cycles through sprite frames on the Tilemap — paints into a Tile Palette like any
// normal tile, but animates (e.g. a scrolling conveyor). Uses the core Tilemap module's
// GetTileAnimationData, so no 2D Tilemap Extras package dependency is needed.
[CreateAssetMenu(fileName = "AnimatedTile2D", menuName = "Tiles/Animated Tile 2D")]
public class AnimatedTile2D : TileBase
{
    [Tooltip("Animation frames, in play order. Frame 0 is the palette/preview sprite.")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("Playback speed in frames per second.")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private Tile.ColliderType colliderType = Tile.ColliderType.Sprite;

    [Header("Conveyor")]
    [Tooltip("Horizontal push (units/s) applied to a player standing on this tile. "
        + "+ = right, − = left, 0 = no push. Match the belt's visible scroll direction.")]
    [SerializeField] private float pushSpeed = 0f;

    // Read by PlayerController2D when the player is grounded on this tile.
    public float PushSpeed => pushSpeed;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = (frames != null && frames.Length > 0) ? frames[0] : null;
        tileData.color = color;
        tileData.colliderType = colliderType;
        tileData.transform = Matrix4x4.identity;
    }

    public override bool GetTileAnimationData(
        Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (frames == null || frames.Length == 0)
        {
            return false;
        }

        tileAnimationData.animatedSprites = frames;
        tileAnimationData.animationSpeed = speed;
        tileAnimationData.animationStartTime = 0f;
        return true;
    }
}
