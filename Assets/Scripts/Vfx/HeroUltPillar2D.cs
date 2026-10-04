using System;
using Castlevania2D.Combat;
using UnityEngine;

namespace Castlevania2D.Vfx
{
    public sealed class HeroUltPillar2D : MonoBehaviour
    {
        [SerializeField] [Min(1f)] private float frameRate = 12f;
        [SerializeField] private Vector2 hitBoxSize = new Vector2(1.15f, 2.4f);

        private Sprite[] frames;
        private SpriteRenderer body;
        private GameObject owner;
        private int damage;
        private int facing;
        private float frameTimer;
        private int index;
        private bool reversing;
        private bool hitApplied;
        private Action<HeroUltPillar2D, int, bool> onFrame;

        public int DisplayFrame { get; private set; } = 1;
        public bool IsReversing => reversing;
        public bool IsFinished { get; private set; }

        public static HeroUltPillar2D Spawn(
            Vector2 groundPoint,
            Sprite[] frames,
            GameObject owner,
            int damage,
            int facing,
            int sortingOrder,
            Action<HeroUltPillar2D, int, bool> onFrame)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            var root = new GameObject("HeroUltPillar");
            root.transform.position = new Vector3(groundPoint.x, groundPoint.y, 0f);
            HeroUltPillar2D pillar = root.AddComponent<HeroUltPillar2D>();
            pillar.Play(frames, owner, damage, facing, sortingOrder, onFrame);
            return pillar;
        }

        public void Play(
            Sprite[] animationFrames,
            GameObject source,
            int hitDamage,
            int facingDirection,
            int sortingOrder,
            Action<HeroUltPillar2D, int, bool> frameCallback)
        {
            frames = animationFrames;
            owner = source;
            damage = Mathf.Max(0, hitDamage);
            facing = facingDirection >= 0 ? 1 : -1;
            onFrame = frameCallback;
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = frames[0];
            body.sortingOrder = sortingOrder;
            body.flipX = facing < 0;
            DisplayFrame = 1;
            onFrame?.Invoke(this, DisplayFrame, false);
        }

        private void Update()
        {
            if (IsFinished || frames == null || frames.Length == 0)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, frameRate);
            if (frameTimer < step)
            {
                return;
            }

            frameTimer -= step;
            if (!reversing)
            {
                if (index < frames.Length - 1)
                {
                    index++;
                    ApplyFrame();
                    return;
                }

                reversing = true;
                if (index > 0)
                {
                    index--;
                }

                ApplyFrame();
                return;
            }

            if (index > 0)
            {
                index--;
                ApplyFrame();
                return;
            }

            IsFinished = true;
            Destroy(gameObject);
        }

        private void ApplyFrame()
        {
            if (body != null)
            {
                body.sprite = frames[index];
            }

            DisplayFrame = index + 1;
            if (!hitApplied && DisplayFrame >= 6)
            {
                hitApplied = true;
                ApplyHit();
            }

            onFrame?.Invoke(this, DisplayFrame, reversing);
        }

        private void ApplyHit()
        {
            if (damage <= 0)
            {
                return;
            }

            Vector2 center = (Vector2)transform.position + new Vector2(0f, hitBoxSize.y * 0.5f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, hitBoxSize, 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                if (owner != null && hit.transform.IsChildOf(owner.transform))
                {
                    continue;
                }

                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.CanReceiveDamage)
                {
                    continue;
                }

                if (damageable is Component component
                    && owner != null
                    && component.transform.root == owner.transform.root)
                {
                    continue;
                }

                Vector2 point = hit.ClosestPoint(transform.position);
                damageable.ReceiveDamage(
                    new DamageInfo(damage, owner, point, new Vector2(facing, 0f)));
            }
        }
    }
}
