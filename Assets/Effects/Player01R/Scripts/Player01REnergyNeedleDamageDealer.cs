using System.Collections.Generic;
using UnityEngine;

public class Player01REnergyNeedleDamageDealer : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float hitRadius = 0.3f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField, Min(0f)] private float physicalDamageAmount = 0f;
    [SerializeField, Min(0f)] private float specialDamageAmount = 0f;

    private readonly HashSet<CombatHealth> hitTargets = new HashSet<CombatHealth>();
    private GameObject source;
    private float healPercentOfDamage;
    private int skillSlotIndex = -1;
    private int runeCastId = -1;
    private Player1Skill_R_NeedleShot ownerSkill;
    private Vector3 previousPosition;
    private bool initialized;

    public void Initialize(
        Player1Skill_R_NeedleShot ownerSkill,
        GameObject source,
        float physicalDamageAmount,
        float specialDamageAmount,
        LayerMask hitLayers,
        int skillSlotIndex,
        int runeCastId,
        float healPercentOfDamage = 0f,
        float hitRadius = 0.3f)
    {
        this.ownerSkill = ownerSkill;
        this.source = source;
        this.physicalDamageAmount = Mathf.Max(0f, physicalDamageAmount);
        this.specialDamageAmount = Mathf.Max(0f, specialDamageAmount);
        this.hitLayers = hitLayers;
        this.skillSlotIndex = skillSlotIndex;
        this.runeCastId = runeCastId;
        this.healPercentOfDamage = Mathf.Clamp01(healPercentOfDamage);
        this.hitRadius = Mathf.Max(0.01f, hitRadius);
        previousPosition = transform.position;
        initialized = true;
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
    }

    private void Update()
    {
        if (!initialized || source == null || (physicalDamageAmount <= 0f && specialDamageAmount <= 0f))
        {
            previousPosition = transform.position;
            return;
        }

        Vector3 currentPosition = transform.position;
        ApplyDamageAlongSegment(previousPosition, currentPosition);
        previousPosition = currentPosition;
    }

    private void ApplyDamageAlongSegment(Vector3 start, Vector3 end)
    {
        Collider[] hits = Physics.OverlapCapsule(
            start,
            end,
            hitRadius,
            hitLayers,
            QueryTriggerInteraction.Collide);

        Transform attacker = source != null ? source.transform : null;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || !BattleTargetUtility.IsMonster(hit, attacker))
            {
                continue;
            }

            CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(hit, attacker);
            if (combatHealth == null || combatHealth.IsDead || !hitTargets.Add(combatHealth))
            {
                continue;
            }

            float beforeEffectiveHealth = ResolveCurrentEffectiveHealth(combatHealth);
            float runeBonusDamage = ConsumeRuneFirstHitBonusDamage();
            float skillDamageTakenMultiplier = PlayerSkillDamageTakenDebuffReceiver.ResolvePlayer01SkillDamageMultiplier(combatHealth.gameObject);
            float resolvedPhysicalDamage = (physicalDamageAmount + runeBonusDamage) * skillDamageTakenMultiplier;
            float resolvedSpecialDamage = specialDamageAmount * skillDamageTakenMultiplier;
            if (resolvedPhysicalDamage > 0f)
            {
                combatHealth.TakeDamage(new BattleDamage(resolvedPhysicalDamage, BattleDamageType.Physical, source));
            }

            if (!combatHealth.IsDead && resolvedSpecialDamage > 0f)
            {
                combatHealth.TakeDamage(new BattleDamage(resolvedSpecialDamage, BattleDamageType.Special, source));
            }

            float actualDamage = Mathf.Max(0f, beforeEffectiveHealth - ResolveCurrentEffectiveHealth(combatHealth));
            bool killedByThisHit = actualDamage > 0f && combatHealth.IsDead;
            ResolveRuneRuntimeState()?.NotifyMonsterDamagedBySkill(skillSlotIndex, combatHealth, actualDamage);

            if (ownerSkill != null)
            {
                ownerSkill.NotifySkillDamageApplied(actualDamage, combatHealth, "R needle");
                ownerSkill.RegisterNeedleDamageResult(combatHealth, actualDamage, killedByThisHit);
            }
            else
            {
                HealSource(actualDamage * healPercentOfDamage);
            }
        }
    }

    private float ConsumeRuneFirstHitBonusDamage()
    {
        RuneRuntimeState runtimeState = ResolveRuneRuntimeState();
        return runtimeState != null ? runtimeState.ConsumeFirstHitBonusDamage(skillSlotIndex, runeCastId) : 0f;
    }

    private RuneRuntimeState ResolveRuneRuntimeState()
    {
        if (source == null)
        {
            return null;
        }

        RuneRuntimeState runtimeState = source.GetComponent<RuneRuntimeState>();
        if (runtimeState != null)
        {
            return runtimeState;
        }

        return source.GetComponentInParent<RuneRuntimeState>();
    }

    private static float ResolveCurrentHealth(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return health.resourceBank != null
            ? Mathf.Max(0f, health.resourceBank.currentHealth)
            : Mathf.Max(0f, health.currentHealth);
    }

    private static float ResolveCurrentEffectiveHealth(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return ResolveCurrentHealth(health) + Mathf.Max(0f, health.GetCurrentShield());
    }

    private void HealSource(float amount)
    {
        if (source == null || amount <= 0f)
        {
            return;
        }

        CombatHealth sourceHealth = source.GetComponent<CombatHealth>();
        if (sourceHealth != null)
        {
            sourceHealth.Heal(amount);
            return;
        }

        BattleResourceBank bank = source.GetComponent<BattleResourceBank>();
        if (bank != null)
        {
            bank.Heal(amount);
        }
    }
}
