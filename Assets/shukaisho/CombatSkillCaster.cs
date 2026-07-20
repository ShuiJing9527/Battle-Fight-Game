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
    private const string DefaultQSkillName = "Q 基础技能";
    private const float DefaultQEnergyCost = 10f;
    private const float DefaultQBaseDamage = 10f;
    private const float DefaultQAttackRange = 2f;

    private const string DefaultWSkillName = "W 防御技能";
    private const float DefaultWEnergyCost = 30f;
    private const float DefaultWBaseDamage = 0f;
    private const float DefaultWAttackRange = 0f;

    private const string DefaultESkillName = "E 位移技能";
    private const float DefaultEEnergyCost = 20f;
    private const float DefaultEBaseDamage = 16f;
    private const float DefaultEAttackRange = 1.2f;

    private const string DefaultRSkillName = "R 终结技能";
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
    [Header("Debug")]
    [SerializeField] private bool debugMeleeHitTrace = false;

    private BattleResourceBank resourceBank;
    private RuneRuntimeState runeRuntimeState;
    private bool isEnsuringDefaultSkills;

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

        if (Application.isPlaying)
        {
            RuntimeRuneScaling.ForceRefresh($"{nameof(CombatSkillCaster)}.{nameof(Awake)}:{name}");
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

    public BattleSkill TryGetSkillRaw(int index)
    {
        if (skills == null || index < 0 || index >= skills.Length)
        {
            return null;
        }

        return skills[index];
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

        runeRuntimeState?.NotifyBaseSkillManaSpent(skillIndex, skill.energyCost);
        int runeCastId = runeRuntimeState != null ? runeRuntimeState.NotifySkillCastStarted(skillIndex) : -1;
        float manaRuneEffectStrength = runeRuntimeState != null ? runeRuntimeState.TriggerManaRuneCastEffect(skillIndex) : 0f;
        ExecuteSkill(skill, skillIndex, runeCastId, manaRuneEffectStrength);
        return true;
    }

    public void RefreshRuneState()
    {
        EnsureDefaultSkills();
        runeRuntimeState?.RebuildFromEquippedRunes();
        if (Application.isPlaying)
        {
            RuntimeRuneScaling.ForceRefresh($"{nameof(CombatSkillCaster)}.{nameof(RefreshRuneState)}:{name}");
        }
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

    private void ExecuteSkill(BattleSkill skill, int skillIndex, int runeCastId, float manaRuneEffectStrength)
    {
        Transform point = attackPoint != null ? attackPoint : transform;
        float baseDamage = Mathf.Max(0f, skill.baseDamage);
        if (resourceBank != null)
        {
            baseDamage *= resourceBank.SkillDamageMultiplier;
        }

        baseDamage *= runeRuntimeState != null ? runeRuntimeState.GetOutgoingDamageMultiplier(skillIndex) : 1f;
        baseDamage *= 1f + Mathf.Max(0f, manaRuneEffectStrength) * 0.5f;

        for (int hit = 0; hit < Mathf.Max(1, skill.hitCount); hit++)
        {
            Collider[] colliders = Physics.OverlapSphere(point.position, skill.attackRange, enemyLayer, QueryTriggerInteraction.Collide);
            HashSet<CombatHealth> hitTargets = new HashSet<CombatHealth>();
            List<string> debugEntries = new List<string>();

            foreach (Collider collider in colliders)
            {
                MonsterIdentity identity = BattleTargetUtility.GetMonsterIdentity(collider);
                if (!BattleTargetUtility.TryGetMonsterCombatHealth(collider, transform, out CombatHealth health, out string rejectReason))
                {
                    debugEntries.Add(BuildMeleeHitDebugEntry(collider, identity, false, rejectReason, 0f, 0f, false, baseDamage, baseDamage));
                    continue;
                }

                if (health != null && hitTargets.Add(health))
                {
                    float resolvedDamage = baseDamage;
                    resolvedDamage += runeRuntimeState != null ? runeRuntimeState.ConsumeFirstHitBonusDamage(skillIndex, runeCastId) : 0f;
                    float finalDamage = BattleStatUtility.ApplyCriticalDamage(gameObject, resolvedDamage, out bool isCritical);
                    float beforeHealth = ResolveTargetCurrentHealth(health);
                    health.TakeDamage(new BattleDamage(finalDamage, damageType, gameObject, isCritical));
                    float afterHealth = ResolveTargetCurrentHealth(health);
                    float actualDamage = Mathf.Max(0f, beforeHealth - afterHealth);
                    runeRuntimeState?.NotifyMonsterDamagedBySkill(skillIndex, health, actualDamage);
                    debugEntries.Add(BuildMeleeHitDebugEntry(collider, identity, true, "None", beforeHealth, afterHealth, true, resolvedDamage, finalDamage));
                }
                else
                {
                    debugEntries.Add(BuildMeleeHitDebugEntry(collider, identity, false, "duplicate-combat-health", 0f, 0f, false, baseDamage, baseDamage));
                }
            }

            if (debugMeleeHitTrace)
            {
                Debug.Log(
                    "[PlayerMeleeHitDebug] " +
                    "skill=" + (skill != null ? skill.skillName : "UnknownSkill") +
                    " attackPosition=" + point.position +
                    " attackRadius=" + skill.attackRange.ToString("F2") +
                    " hitColliderCount=" + colliders.Length +
                    " hitIndex=" + hit +
                    " details=" + (debugEntries.Count > 0 ? string.Join(" | ", debugEntries) : "none"),
                    this);
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

    private static string BuildMeleeHitDebugEntry(
        Collider collider,
        MonsterIdentity identity,
        bool acceptedTarget,
        string rejectReason,
        float beforeHealth,
        float afterHealth,
        bool takeDamageCalled,
        float damageBeforeModifiers,
        float damageAfterModifiers)
    {
        Transform root = collider != null ? collider.transform.root : null;
        float actualDamage = Mathf.Max(0f, beforeHealth - afterHealth);

        return
            "collider=" + (collider != null ? collider.name : "null") +
            " root=" + (root != null ? root.name : "null") +
            " layer=" + (collider != null ? LayerMask.LayerToName(collider.gameObject.layer) : "null") +
            " tag=" + (collider != null ? collider.tag : "null") +
            " hasCombatHealth=" + takeDamageCalled +
            " hasMonsterIdentity=" + (identity != null) +
            " rank=" + (identity != null ? identity.rank.ToString() : "Unknown") +
            " isBoss=" + (identity != null && identity.rank == MonsterRank.Boss) +
            " acceptedTarget=" + acceptedTarget +
            " rejectReason=" + rejectReason +
            " damageBeforeModifiers=" + damageBeforeModifiers.ToString("F2") +
            " damageAfterModifiers=" + damageAfterModifiers.ToString("F2") +
            " TakeDamageCalled=" + takeDamageCalled +
            " actualDamage=" + actualDamage.ToString("F2");
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
        EnsureDefaultSkills(true);
    }

    private void EnsureDefaultSkills(bool rebuildRuneState)
    {
        if (isEnsuringDefaultSkills)
        {
            return;
        }

        isEnsuringDefaultSkills = true;
        try
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

        if (rebuildRuneState && runeRuntimeState != null)
        {
            runeRuntimeState.RebuildFromEquippedRunes();
            if (Application.isPlaying)
            {
                RuntimeRuneScaling.ForceRefresh($"{nameof(CombatSkillCaster)}.{nameof(EnsureDefaultSkills)}:{name}");
            }
        }
        }
        finally
        {
            isEnsuringDefaultSkills = false;
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
