using UnityEngine;

namespace Castlevania2D.ScriptableObjects
{
    [CreateAssetMenu(menuName = "Castlevania 2D/Character Stats")]
    public sealed class CharacterStats : ScriptableObject
    {
        [SerializeField] private int maxHealth = 5;
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float jumpVelocity = 12f;
        [SerializeField] private int contactDamage = 1;

        public int MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float JumpVelocity => jumpVelocity;
        public int ContactDamage => contactDamage;
    }
}
