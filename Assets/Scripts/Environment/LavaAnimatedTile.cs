using UnityEngine;
using UnityEngine.Tilemaps;

namespace Castlevania2D.Environment
{
    /// <summary>
    /// Animated visual lava tile. Damage and trigger behavior belong to a separate component.
    /// </summary>
    [CreateAssetMenu(fileName = "Lava_Animated", menuName = "Castlevania 2D/Tiles/Lava Animated Tile")]
    public sealed class LavaAnimatedTile : TileBase
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(0.01f)] private float framesPerSecond = 10f;

        public override void GetTileData(
            Vector3Int position,
            ITilemap tilemap,
            ref TileData tileData)
        {
            tileData.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockColor | TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.Sprite;
        }

        public override bool GetTileAnimationData(
            Vector3Int position,
            ITilemap tilemap,
            ref TileAnimationData tileAnimationData)
        {
            if (frames == null || frames.Length == 0)
            {
                return false;
            }

            tileAnimationData.animatedSprites = frames;
            tileAnimationData.animationSpeed = framesPerSecond;
            tileAnimationData.animationStartTime = 0f;
            return true;
        }
    }
}
