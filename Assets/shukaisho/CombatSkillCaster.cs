using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BattleSkill
{
    public const int DefaultRuneSlotCount = 5;

    public string skillName;
    public BattleSkillType skillType = BattleSkillType.SmallSkill;
    [Min(0f)] public float energyCost = 10f;
    [Min(0)] public int runeSlotCount = DefaultRuneSlotCount;
    [Min(1)] public int hitCount = 1;
    [Min(0f)] public float baseDamage = 1f;
    [Min(0f)] public float attackRange = 1.5f;
    public RuneDefinition[] equippedRunes = new RuneDefinition[DefaultRuneSlotCount];
}

public class CombatSkillCaster : MonoBehaviour
{
    private const string DefaultQSkillName = "Q Basic Skill";
    private const float DefaultQEnergyCost = 10f;
    private const float DefaultQBaseDamage = 10f;
    private const float DefaultQAttackRange = 2f;

    private const string DefaultWSkillName = "W Defense Skill";
    private const float DefaultWEnergyCost = 30f;
    private const float DefaultWBaseDamage = 0f;
    private const float DefaultWAttackRange = 0f;

    private const string DefaultESkillName = "E Movement Skill";
    private const float DefaultEEnergyCost = 20f;
    private const float DefaultEBaseDamage = 16f;
    private const float DefaultEAttackRange = 1.2f;

    private const string DefaultRSkillName = "R Ultimate Skill";
    private const float DefaultREnergyCost = 60f;
    private const float DefaultRBaseDamage = 50f;
    private const float DefaultRAttackRange = 6f;

    private const int SkillCount = 4;

    [Header("Skill slots: Q/W/E/R")]
    public BattleSkill[] skills = new BattleSkill[SkillCount];

    [Header("Attack Target")]
    public Transform attackPoint;
    public LayerMask enemyLayer = ~0;
    public BattleDamageType damageType = BattleDamageType.Physical;

    private BattleResourceBank resourceBank;
    private RuneRuntimeState runeRuntimeState;

    private void Reset()
    {
        LoadDefaultSkills();
    }

    private void Awake()
    {
        EnsureDefaultSkills();
        resourceBank = GetComponent<BattleResourceBank>();
        runeRuntimeState = GetComponent<RuneRuntimeState>();
        if (runeRuntimeState == null)
        {
            runeRuntimeState = gameObject.AddComponent<RuneRuntimeState>();
        }

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

        return CastSkill(skills[index], index);
    }

    public bool CastSkill(BattleSkill skill)
    {
        int skillIndex = GetSkillIndex(skill);
        return CastSkill(skill, skillIndex);
    }

    public BattleSkill GetSkill(int index)
    {
        EnsureDefaultSkills();
        return index >= 0 && index < skills.Length ? skills[index] : null;
    }

    public bool CastSkill(BattleSkill skill, int skillIndex)
    {
        if (skill == null)
        {
            return false;
        }

        if (resourceBank != null && skill.energyCost > 0f && !resourceBank.TrySpendEnergy(skill.energyCost))
        {
            return false;
        }

        runeRuntimeState?.NotifySkillCastStarted(skillIndex);
        ExecuteSkill(skill, skillIndex);
        return true;
    }

    public void RefreshRuneState()
    {
        EnsureDefaultSkills();
        runeRuntimeState?.RebuildFromEquippedRunes();
    }

    public void ExecuteThornCounter(int skillIndex, CombatHealth target)
    {
        if (target == null)
        {
            return;
        }

        BattleSkill skill = GetSkill(skillIndex);
        if (skill == null)
        {
            return;
        }

        float damage = Mathf.Max(0f, skill.baseDamage);
        if (resourceBank != null)
        {
            damage *= resourceBank.SkillDamageMultiplier;
        }

        damage *= runeRuntimeState != null ? runeRuntimeState.GetOutgoingDamageMultiplier(skillIndex) : 1f;
        float finalDamage = BattleStatUtility.ApplyCriticalDamage(gameObject, damage, out bool isCritical);
        target.TakeDamage(new BattleDamage(finalDamage, damageType, gameObject, isCritical));
    }

    private void ExecuteSkill(BattleSkill skill, int skillIndex)
    {
        Transform point = attackPoint != null ? attackPoint : transform;
        float baseDamage = Mathf.Max(0f, skill.baseDamage);
        if (resourceBank != null)
        {
            baseDamage *= resourceBank.SkillDamageMultiplier;
        }

        baseDamage *= runeRuntimeState != null ? runeRuntimeState.GetOutgoingDamageMultiplier(skillIndex) : 1f;

        for (int hit = 0; hit < Mathf.Max(1, skill.hitCount); hit++)
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
                    float resolvedDamage = baseDamage;
                    resolvedDamage += runeRuntimeState != null ? runeRuntimeState.ConsumeFirstHitBonusDamage(skillIndex) : 0f;
                    float finalDamage = BattleStatUtility.ApplyCriticalDamage(gameObject, resolvedDamage, out bool isCritical);
                    float beforeHealth = ResolveTargetCurrentHealth(health);
                    health.TakeDamage(new BattleDamage(finalDamage, damageType, gameObject, isCritical));
                    float actualDamage = Mathf.Max(0f, beforeHealth - ResolveTargetCurrentHealth(health));
                    runeRuntimeState?.NotifyMonsterDamagedBySkill(skillIndex, health, actualDamage);
                }
                else if (legacyHealth != null)
                {
                    legacyHealth.TakeDamage(Mathf.RoundToInt(baseDamage), gameObject);
                }
            }
        }
    }

    private float ResolveTargetCurrentHealth(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return health.resourceBank != null
            ? Mathf.Max(0f, health.resourceBank.currentHealth)
            : Mathf.Max(0f, health.currentHealth);
    }

    private int GetSkillIndex(BattleSkill skill)
    {
        if (skill == null || skills == null)
        {
            return -1;
        }

        for (int i = 0; i < skills.Length; i++)
        {
            if (ReferenceEquals(skills[i], skill))
            {
                return i;
            }
        }

        return -1;
    }

    private void EnsureDefaultSkills()
    {
        if (skills == null || skills.Length != SkillCount)
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
                RuneDefinition[] previous = skills[i].equippedRunes;
                skills[i].equippedRunes = new RuneDefinition[slotCount];
                if (previous != null)
                {
                    int copyLength = Mathf.Min(previous.Length, skills[i].equippedRunes.Length);
                    for (int slotIndex = 0; slotIndex < copyLength; slotIndex++)
                    {
                        skills[i].equippedRunes[slotIndex] = SanitizeRuneReference(previous[slotIndex]);
                    }
                }
            }
            else
            {
                for (int slotIndex = 0; slotIndex < skills[i].equippedRunes.Length; slotIndex++)
                {
                    skills[i].equippedRunes[slotIndex] = SanitizeRuneReference(skills[i].equippedRunes[slotIndex]);
                }
            }
        }

        if (runeRuntimeState != null)
        {
            runeRuntimeState.RebuildFromEquippedRunes();
        }
    }

    private RuneDefinition SanitizeRuneReference(RuneDefinition rune)
    {
        if (rune == null)
        {
            return null;
        }

        return rune.IsConfigured() ? rune : null;
    }

    private void LoadDefaultSkills()
    {
        skills = new[]
        {
            new BattleSkill { skillName = DefaultQSkillName, skillType = BattleSkillType.SmallSkill, energyCost = DefaultQEnergyCost, runeSlotCount = BattleSkill.DefaultRuneSlotCount, hitCount = 1, baseDamage = DefaultQBaseDamage, attackRange = DefaultQAttackRange },
            new BattleSkill { skillName = DefaultWSkillName, skillType = BattleSkillType.SmallSkill, energyCost = DefaultWEnergyCost, runeSlotCount = BattleSkill.DefaultRuneSlotCount, hitCount = 1, baseDamage = DefaultWBaseDamage, attackRange = DefaultWAttackRange },
            new BattleSkill { skillName = DefaultESkillName, skillType = BattleSkillType.SmallSkill, energyCost = DefaultEEnergyCost, runeSlotCount = BattleSkill.DefaultRuneSlotCount, hitCount = 1, baseDamage = DefaultEBaseDamage, attackRange = DefaultEAttackRange },
            new BattleSkill { skillName = DefaultRSkillName, skillType = BattleSkillType.Ultimate, energyCost = DefaultREnergyCost, runeSlotCount = BattleSkill.DefaultRuneSlotCount, hitCount = 1, baseDamage = DefaultRBaseDamage, attackRange = DefaultRAttackRange }
        };
    }
}
