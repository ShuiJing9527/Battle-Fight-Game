using UnityEngine;

public class CombatStats : MonoBehaviour
{
    [Header("角色属性表")]
    [Min(0f)] public float maxHealth = 3f;
    [Min(0f)] public float physicalAttack = 1f;
    [Min(0f)] public float physicalDefense = 0f;
    [Min(0f)] public float specialAttack = 1f;
    [Min(0f)] public float specialDefense = 0f;
    [Min(0f)] public float speed = 1f;
    [Min(0f)] public float luck = 0f;

    public float ReduceDamage(BattleDamage damage)
    {
        float defense = damage.damageType == BattleDamageType.Physical ? physicalDefense : specialDefense;
        return Mathf.Max(0f, damage.amount - defense);
    }
}
