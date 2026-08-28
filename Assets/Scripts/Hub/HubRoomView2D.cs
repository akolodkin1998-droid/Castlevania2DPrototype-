using Castlevania2D.Player;
using UnityEngine;

namespace Castlevania2D.Hub
{
    public sealed class HubRoomView2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer frameSprite;
        [SerializeField] private float padding;

        public void Apply(CameraFollow2D cameraFollow)
        {
            if (cameraFollow == null)
            {
                return;
            }

            if (frameSprite == null)
            {
                cameraFollow.EnterLockedView(transform.position, 6f);
                return;
            }

            Bounds bounds = frameSprite.bounds;
            if (padding > 0f)
            {
                bounds.Expand(padding * 2f);
            }

            cameraFollow.EnterFramedSpriteView(bounds);
        }

#if UNITY_EDITOR
        public void EditorAssign(SpriteRenderer frame)
        {
            frameSprite = frame;
        }
#endif
    }
}
