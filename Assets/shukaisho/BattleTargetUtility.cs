using UnityEngine;

public static class BattleTargetUtility
{
    public static bool IsPlayer(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        return target.GetComponentInParent<PlayerMovement>() != null ||
               target.GetComponentInParent<Player01SkillController>() != null ||
               target.GetComponentInParent<Player2PrototypeController>() != null ||
               target.CompareTag("Player");
    }

    public static bool IsMonster(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (IsPlayer(target))
        {
            return false;
        }

        return target.GetComponentInParent<MonsterIdentity>() != null ||
               target.GetComponentInParent<EnemyController>() != null;
    }

    public static bool IsMonster(Collider collider, Transform attacker)
    {
        if (collider == null)
        {
            return false;
        }

        if (attacker != null && collider.transform.IsChildOf(attacker))
        {
            return false;
        }

        if (IsPlayer(collider.gameObject))
        {
            return false;
        }

        return IsMonster(collider.gameObject);
    }

    public static CombatHealth GetMonsterCombatHealth(Collider collider, Transform attacker)
    {
        return TryGetMonsterCombatHealth(collider, attacker, out CombatHealth health, out _) ? health : null;
    }

    public static bool TryGetMonsterCombatHealth(Collider collider, Transform attacker, out CombatHealth health, out string rejectReason)
    {
        health = null;

        if (collider == null)
        {
            rejectReason = "null-collider";
            return false;
        }

        if (attacker != null && collider.transform.IsChildOf(attacker))
        {
            rejectReason = "self-collider";
            return false;
        }

        if (IsPlayer(collider.gameObject))
        {
            rejectReason = "player-collider";
            return false;
        }

        MonsterIdentity identity = collider.GetComponentInParent<MonsterIdentity>();
        EnemyController enemyController = collider.GetComponentInParent<EnemyController>();
        if (identity == null && enemyController == null)
        {
            rejectReason = "not-monster";
            return false;
        }

        health = collider.GetComponentInParent<CombatHealth>();
        if (health == null)
        {
            rejectReason = "missing-combat-health";
            return false;
        }

        if (health.IsDead)
        {
            rejectReason = "target-dead";
            return false;
        }

        rejectReason = null;
        return true;
    }

    public static MonsterIdentity GetMonsterIdentity(Collider collider)
    {
        return collider != null ? collider.GetComponentInParent<MonsterIdentity>() : null;
    }
}
