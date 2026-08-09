using Castlevania2D.Health;
using UnityEngine;

/// <summary>
/// Stationary snake: idle sprite until the player overlaps its trigger,
/// then plays attack frames once (forward then reverse) and instantly kills the player.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class SnakeContactEnemy2D : MonoBehaviour
{
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] attackSprites;
    [SerializeField] private float attackFrameRate = 12f;
    [SerializeField] private bool faceLeft = true;
    [SerializeField] private bool killPlayerOnContact = true;
    [SerializeField] private bool playAttackOnContact = true;
    [SerializeField] private bool loopAttackPingPong = false;

    private SpriteRenderer spriteRenderer;
    private Collider2D hitCollider;
    private Health health;
    private bool attacking;
    private bool hasKilledPlayer;
    private int attackFrameIndex;
    private int attackDirection = 1;
    private float frameTimer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitCollider = GetComponent<Collider2D>();
        hitCollider.isTrigger = true;
        health = GetComponent<Health>();

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = faceLeft;
            if (idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }

        SyncColliderToSprite();
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null)
        {
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= OnDied;
        }
    }

    private void OnDied()
    {
        // Belt-and-suspenders with Health.destroyOnDeath: hide immediately on death.
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!attacking || attackSprites == null || attackSprites.Length == 0)
        {
            return;
        }

        if (attackSprites.Length == 1)
        {
            spriteRenderer.sprite = attackSprites[0];
            return;
        }

        float frameDuration = 1f / Mathf.Max(1f, attackFrameRate);
        frameTimer += Time.deltaTime;
        while (frameTimer >= frameDuration && attacking)
        {
            frameTimer -= frameDuration;
            AdvanceAttackPingPong();
            if (!attacking)
            {
                break;
            }

            spriteRenderer.sprite = attackSprites[attackFrameIndex];
            SyncColliderToSprite();
        }
    }

    private void AdvanceAttackPingPong()
    {
        attackFrameIndex += attackDirection;

        if (attackFrameIndex >= attackSprites.Length - 1)
        {
            attackFrameIndex = attackSprites.Length - 1;
            attackDirection = -1;
            return;
        }

        if (attackFrameIndex > 0)
        {
            return;
        }

        attackFrameIndex = 0;
        if (loopAttackPingPong)
        {
            attackDirection = 1;
            return;
        }

        // One forward+reverse pass completed → idle.
        attacking = false;
        if (idleSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = idleSprite;
            SyncColliderToSprite();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStrikePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Catch cases where overlap already exists when the snake is enabled.
        TryStrikePlayer(other);
    }

    private void TryStrikePlayer(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        Health playerHealth = ResolvePlayerHealth(other);
        if (playerHealth == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (playAttackOnContact && !attacking && attackSprites != null && attackSprites.Length > 0)
        {
            BeginAttack();
        }

        if (killPlayerOnContact && !hasKilledPlayer)
        {
            hasKilledPlayer = true;
            playerHealth.Kill();
        }
    }

    private void BeginAttack()
    {
        attacking = true;
        attackFrameIndex = 0;
        attackDirection = 1;
        frameTimer = 0f;
        spriteRenderer.sprite = attackSprites[0];
        SyncColliderToSprite();
    }

    private static Health ResolvePlayerHealth(Collider2D other)
    {
        HeroKnight hero = other.GetComponentInParent<HeroKnight>();
        if (hero != null)
        {
            return hero.GetComponent<Health>();
        }

        // Fallback: Health on a root named like the prototype player.
        Health health = other.GetComponentInParent<Health>();
        if (health == null)
        {
            return null;
        }

        Transform root = health.transform;
        string name = root.name;
        if (name.IndexOf("Hero", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return health;
        }

        return null;
    }

    private void SyncColliderToSprite()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        if (hitCollider is BoxCollider2D box)
        {
            Bounds bounds = spriteRenderer.sprite.bounds;
            box.isTrigger = true;
            box.size = bounds.size;
            box.offset = bounds.center;
        }
    }

#if UNITY_EDITOR
    public void EditorAssignSprites(Sprite idle, Sprite[] attack)
    {
        idleSprite = idle;
        attackSprites = attack ?? System.Array.Empty<Sprite>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null && idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
        }

        SyncColliderToSprite();
    }
#endif
}
