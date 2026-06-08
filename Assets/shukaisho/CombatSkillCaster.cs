using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BattleSkill
{
    [Header("技能系统表")]
    public string skillName;
    public BattleSkillType skillType = BattleSkillType.SmallSkill;
    [Min(0f)] public float energyCost = 10f;
    [Min(0)] public int runeSlotCount = 5;

    [Header("技能示例表")]
    [Min(1)] public int hitCount = 1;
    [Min(0f)] public float baseDamage = 1f;
    [Min(0f)] public float attackRange = 1.5f;
    public RuneDefinition[] equippedRunes = new RuneDefinition[5];
}

public class CombatSkillCaster : MonoBehaviour
{
    [Header("技能数量：2小技能 + 1大招")]
    public BattleSkill[] skills = new BattleSkill[3];

    [Header("释放目标")]
    public Transform attackPoint;
    public LayerMask enemyLayer;
    public BattleDamageType damageType = BattleDamageType.Physical;

    private BattleResourceBank resourceBank;

    private void Reset()
    {
        skills = new[]
        {
            new BattleSkill { skillName = "小技能1", skillType = BattleSkillType.SmallSkill, energyCost = 10f, runeSlotCount = 5, hitCount = 1, baseDamage = 1f },
            new BattleSkill { skillName = "小技能2", skillType = BattleSkillType.SmallSkill, energyCost = 10f, runeSlotCount = 5, hitCount = 1, baseDamage = 1f },
            new BattleSkill { skillName = "大招", skillType = BattleSkillType.Ultimate, energyCost = 100f, runeSlotCount = 5, hitCount = 1, baseDamage = 1f }
        };
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

    private void EnsureDefaultSkills()
    {
        if (skills == null || skills.Length != 3)
        {
            Reset();
        }

        for (int i = 0; i < skills.Length; i++)
        {
            if (skills[i] == null)
            {
                Reset();
                break;
            }

            if (skills[i].equippedRunes == null || skills[i].equippedRunes.Length != skills[i].runeSlotCount)
            {
                skills[i].equippedRunes = new RuneDefinition[Mathf.Max(0, skills[i].runeSlotCount)];
            }
        }
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
        if (!PayCost(skill, plan))
        {
            return false;
        }

        StartCoroutine(CastRoutine(skill, plan));
        return true;
    }

    private bool PayCost(BattleSkill skill, SkillRuntimePlan plan)
    {
        if (skill.energyCost > 0f && resourceBank == null)
        {
            return false;
        }

        if (resourceBank != null && !resourceBank.TrySpendEnergy(skill.energyCost))
        {
            return false;
        }

        if (plan.healthCost > 0f)
        {
            BattleResourceBank bank = resourceBank != null ? resourceBank : GetComponent<BattleResourceBank>();
            if (bank == null || !bank.TrySpendHealth(plan.healthCost))
            {
                return false;
            }
        }

        return true;
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
            damage *= resourceBank.SkillDamageMultiplier;
        }

        for (int hit = 0; hit < plan.hitCount; hit++)
        {
            Collider[] colliders = Physics.OverlapSphere(point.position, skill.attackRange, enemyLayer);
            List<CombatHealth> hitTargets = new List<CombatHealth>();

            foreach (Collider collider in colliders)
            {
                CombatHealth health = collider.GetComponentInParent<CombatHealth>();
                EnemyHealth legacyHealth = collider.GetComponentInParent<EnemyHealth>();

                if (health != null)
                {
                    health.TakeDamage(new BattleDamage(damage, damageType, gameObject));
                    hitTargets.Add(health);
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

        Collider[] colliders = Physics.OverlapSphere(sourceTarget.transform.position, plan.splitRange, enemyLayer);
        HashSet<CombatHealth> damagedTargets = new HashSet<CombatHealth>();

        foreach (Collider collider in colliders)
        {
            CombatHealth target = collider.GetComponentInParent<CombatHealth>();
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

    private SkillRuntimePlan BuildPlan(BattleSkill skill)
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
                plan.healthCost += rune.healthCost;
                plan.exchangeHeal += rune.healAmount;
                break;
            case RuneMechanic.SoulLink:
                plan.soulLinkHeal += rune.healAmount;
                break;
        }
    }

    private struct SkillRuntimePlan
    {
        public int hitCount;
        public int castCount;
        public float damageMultiplier;
        public float afterimageDelaySeconds;
        public float splitRange;
        public float echoDelaySeconds;
        public float healthCost;
        public float exchangeHeal;
        public float drainMarkHeal;
        public float regenerationHeal;
        public float soulLinkHeal;
        public SoulPickup bloodExplosionLifeSoulPrefab;
    }
}
