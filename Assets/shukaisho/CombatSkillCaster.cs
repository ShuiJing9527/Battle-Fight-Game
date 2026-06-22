using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BattleSkill
{
    public string skillName;
    public BattleSkillType skillType = BattleSkillType.SmallSkill;
    [Min(0f)] public float energyCost = 10f;
    [Min(0)] public int runeSlotCount = 5;
    [Min(1)] public int hitCount = 1;
    [Min(0f)] public float baseDamage = 1f;
    [Min(0f)] public float attackRange = 1.5f;
    public RuneDefinition[] equippedRunes = new RuneDefinition[5];
}

public class CombatSkillCaster : MonoBehaviour
{
    [Header("Skill slots: Q/W/E/R")]
    public BattleSkill[] skills = new BattleSkill[4];

    [Header("Attack Target")]
    public Transform attackPoint;
    public LayerMask enemyLayer = ~0;
    public BattleDamageType damageType = BattleDamageType.Physical;

    private BattleResourceBank resourceBank;

    private void Reset()
    {
        LoadDefaultSkills();
    }

    private void Awake()
    {
        EnsureDefaultSkills();
        resourceBank = GetComponent<BattleResourceBank>();
        if (attackPoint == null)
        {
            attackPoint = transform;
        }
    }

    private void OnValidate()
    {
        EnsureDefaultSkills();
    }

    public bool CastSkill(int index)
    {
        if (skills == null || index < 0 || index >= skills.Length || skills[index] == null)
        {
            return false;
        }

        return CastSkill(skills[index]);
    }

    public bool CastSkill(BattleSkill skill)
    {
        if (skill == null)
        {
            return false;
        }

        SkillRuntimePlan plan = BuildPlan(skill);
        if (resourceBank != null && skill.energyCost > 0f && !resourceBank.TrySpendEnergy(skill.energyCost))
        {
            return false;
        }

        StartCoroutine(CastRoutine(skill, plan));
        return true;
    }

    public SkillRuntimePlan BuildPlan(BattleSkill skill)
    {
        SkillRuntimePlan plan = new SkillRuntimePlan
        {
            hitCount = Mathf.Max(1, skill.hitCount),
            castCount = 1,
            damageMultiplier = 1f
        };

        int slotLimit = Mathf.Min(skill.runeSlotCount, skill.equippedRunes == null ? 0 : skill.equippedRunes.Length);
        for (int i = 0; i < slotLimit; i++)
        {
            RuneDefinition rune = skill.equippedRunes[i];
            if (rune == null)
            {
                continue;
            }

            ApplyRuneToPlan(rune, ref plan);
        }

        return plan;
    }

    public BattleSkill GetSkill(int index)
    {
        EnsureDefaultSkills();
        return index >= 0 && index < skills.Length ? skills[index] : null;
    }

    private IEnumerator CastRoutine(BattleSkill skill, SkillRuntimePlan plan)
    {
        for (int cast = 0; cast < plan.castCount; cast++)
        {
            ExecuteSkill(skill, plan);
            yield return null;
        }

        if (plan.afterimageDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(plan.afterimageDelaySeconds);
            ExecuteSkill(skill, plan);
        }
    }

    private void ExecuteSkill(BattleSkill skill, SkillRuntimePlan plan)
    {
        Transform point = attackPoint != null ? attackPoint : transform;
        float damage = skill.baseDamage * plan.damageMultiplier;
        if (resourceBank != null)
        {
            damage *= resourceBank.SkillDamageMultiplier * resourceBank.AttributeDamageMultiplier;
        }

        for (int hit = 0; hit < plan.hitCount; hit++)
        {
            Collider[] colliders = Physics.OverlapSphere(point.position, skill.attackRange, enemyLayer, QueryTriggerInteraction.Collide);
            HashSet<CombatHealth> hitTargets = new HashSet<CombatHealth>();

            foreach (Collider collider in colliders)
            {
                if (!BattleTargetUtility.IsMonster(collider, transform))
                {
                    continue;
                }

                CombatHealth health = BattleTargetUtility.GetMonsterCombatHealth(collider, transform);
                EnemyHealth legacyHealth = BattleTargetUtility.GetMonsterLegacyHealth(collider, transform);

                if (health != null && hitTargets.Add(health))
                {
                    health.TakeDamage(new BattleDamage(damage, damageType, gameObject));
                    ApplyOnHitRunes(health, point.position, damage, plan);
                    ApplySplitDamage(health, damage, plan);
                }
                else if (legacyHealth != null)
                {
                    legacyHealth.TakeDamage(Mathf.RoundToInt(damage), gameObject);
                }
            }

            if (hitTargets.Count > 1 && plan.soulLinkHeal > 0f && resourceBank != null)
            {
                resourceBank.Heal(plan.soulLinkHeal * hitTargets.Count);
            }
        }
    }

    private void ApplyOnHitRunes(CombatHealth target, Vector3 hitPosition, float damage, SkillRuntimePlan plan)
    {
        if (plan.drainMarkHeal > 0f)
        {
            DrainMark mark = target.GetComponent<DrainMark>();
            if (mark == null)
            {
                mark = target.gameObject.AddComponent<DrainMark>();
            }

            mark.Set(gameObject, plan.drainMarkHeal);
        }

        if (plan.regenerationHeal > 0f)
        {
            RegenerationArea.Create(hitPosition, plan.regenerationHeal);
        }

        if (plan.exchangeHeal > 0f && resourceBank != null)
        {
            resourceBank.Heal(plan.exchangeHeal);
        }

        if (plan.echoDelaySeconds > 0f)
        {
            StartCoroutine(EchoDamage(target, damage, plan.echoDelaySeconds));
        }

        if (plan.bloodExplosionLifeSoulPrefab != null)
        {
            BloodExplosionDrop drop = target.GetComponent<BloodExplosionDrop>();
            if (drop == null)
            {
                drop = target.gameObject.AddComponent<BloodExplosionDrop>();
            }

            drop.Set(plan.bloodExplosionLifeSoulPrefab);
        }
    }

    private void ApplySplitDamage(CombatHealth sourceTarget, float damage, SkillRuntimePlan plan)
    {
        if (sourceTarget == null || plan.splitRange <= 0f)
        {
            return;
        }

        Collider[] colliders = Physics.OverlapSphere(sourceTarget.transform.position, plan.splitRange, enemyLayer, QueryTriggerInteraction.Collide);
        HashSet<CombatHealth> damagedTargets = new HashSet<CombatHealth>();

        foreach (Collider collider in colliders)
        {
            if (!BattleTargetUtility.IsMonster(collider, transform))
            {
                continue;
            }

            CombatHealth target = BattleTargetUtility.GetMonsterCombatHealth(collider, transform);
            if (target == null || target == sourceTarget || damagedTargets.Contains(target))
            {
                continue;
            }

            target.TakeDamage(new BattleDamage(damage, damageType, gameObject));
            damagedTargets.Add(target);
        }
    }

    private IEnumerator EchoDamage(CombatHealth target, float damage, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null)
        {
            target.TakeDamage(new BattleDamage(damage, damageType, gameObject));
        }
    }

    private void ApplyRuneToPlan(RuneDefinition rune, ref SkillRuntimePlan plan)
    {
        switch (rune.mechanic)
        {
            case RuneMechanic.Combo:
                plan.hitCount += rune.extraHitCount;
                plan.damageMultiplier *= rune.damageMultiplier;
                break;
            case RuneMechanic.DoubleStar:
                plan.castCount *= Mathf.Max(1, rune.extraCastCount);
                plan.damageMultiplier *= rune.damageMultiplier;
                break;
            case RuneMechanic.Afterimage:
                plan.afterimageDelaySeconds = Mathf.Max(plan.afterimageDelaySeconds, rune.delaySeconds);
                break;
            case RuneMechanic.Split:
                plan.splitRange = Mathf.Max(plan.splitRange, rune.range);
                break;
            case RuneMechanic.Echo:
                plan.echoDelaySeconds = Mathf.Max(plan.echoDelaySeconds, rune.delaySeconds);
                break;
            case RuneMechanic.BloodExplosion:
                plan.bloodExplosionLifeSoulPrefab = rune.lifeSoulPrefab;
                break;
            case RuneMechanic.DrainMark:
                plan.drainMarkHeal += rune.healAmount;
                break;
            case RuneMechanic.Regeneration:
                plan.regenerationHeal += rune.healAmount;
                break;
            case RuneMechanic.Exchange:
                plan.exchangeHeal += rune.healAmount;
                break;
            case RuneMechanic.SoulLink:
                plan.soulLinkHeal += rune.healAmount;
                break;
        }
    }

    private void EnsureDefaultSkills()
    {
        if (skills == null || skills.Length != 4)
        {
            LoadDefaultSkills();
        }

        for (int i = 0; i < skills.Length; i++)
        {
            if (skills[i] == null)
            {
                LoadDefaultSkills();
                break;
            }

            int slotCount = Mathf.Max(0, skills[i].runeSlotCount);
            if (skills[i].equippedRunes == null || skills[i].equippedRunes.Length != slotCount)
            {
                skills[i].equippedRunes = new RuneDefinition[slotCount];
            }
        }
    }

    private void LoadDefaultSkills()
    {
        skills = new[]
        {
            new BattleSkill { skillName = "Q Basic Skill", skillType = BattleSkillType.SmallSkill, energyCost = 10f, runeSlotCount = 5, hitCount = 1, baseDamage = 10f, attackRange = 2f },
            new BattleSkill { skillName = "W Defense Skill", skillType = BattleSkillType.SmallSkill, energyCost = 30f, runeSlotCount = 5, hitCount = 1, baseDamage = 0f, attackRange = 0f },
            new BattleSkill { skillName = "E Movement Skill", skillType = BattleSkillType.SmallSkill, energyCost = 20f, runeSlotCount = 5, hitCount = 1, baseDamage = 16f, attackRange = 1.2f },
            new BattleSkill { skillName = "R Ultimate Skill", skillType = BattleSkillType.Ultimate, energyCost = 60f, runeSlotCount = 5, hitCount = 1, baseDamage = 50f, attackRange = 6f }
        };
    }

    public struct SkillRuntimePlan
    {
        public int hitCount;
        public int castCount;
        public float damageMultiplier;
        public float afterimageDelaySeconds;
        public float splitRange;
        public float echoDelaySeconds;
        public float exchangeHeal;
        public float drainMarkHeal;
        public float regenerationHeal;
        public float soulLinkHeal;
        public SoulPickup bloodExplosionLifeSoulPrefab;
    }
}
