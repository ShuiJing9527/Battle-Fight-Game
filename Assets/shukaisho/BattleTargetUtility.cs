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
               target.GetComponentInParent<EnemyController>() != null ||
               target.GetComponentInParent<EnemyHealth>() != null;
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
        return IsMonster(collider, attacker) ? collider.GetComponentInParent<CombatHealth>() : null;
    }

    public static EnemyHealth GetMonsterLegacyHealth(Collider collider, Transform attacker)
    {
        return IsMonster(collider, attacker) ? collider.GetComponentInParent<EnemyHealth>() : null;
    }
}
