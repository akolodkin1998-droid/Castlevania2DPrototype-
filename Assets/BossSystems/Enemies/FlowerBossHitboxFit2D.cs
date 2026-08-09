using UnityEngine;

namespace Castlevania2D.Enemies
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FlowerBossHitboxFit2D : MonoBehaviour
    {
        [SerializeField] private Vector2 hitboxSize = new Vector2(5f, 7f);
        [SerializeField] private Vector2 hitboxOffset = Vector2.zero;
        [SerializeField] private bool isTrigger = true;

        private void Awake()
        {
            ApplyFit();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyFit();
        }
#endif

        public void ApplyFit()
        {
            BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                return;
            }

            boxCollider.offset = hitboxOffset;
            boxCollider.size = hitboxSize;
            boxCollider.isTrigger = isTrigger;
        }
    }
}
