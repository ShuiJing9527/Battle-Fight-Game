using System.Collections;
using UnityEngine;
using Spine;
using Spine.Unity;
using UnityEngine.Serialization;

public class Player1Skill_Q_QuickShear : Player01SkillBase
{
    [Header("Q")]
    [SerializeField, Min(1)] private int slashCount = 3;
    [SerializeField, Min(0f)] private float slashInterval = 0.15f;
    [FormerlySerializedAs("qDamage")]
    [SerializeField, Min(0f)] private float baseDamage = 20f;
    [SerializeField, Min(0f)] private float physicalScaling = 0.6f;
    [SerializeField, Min(0f)] private float specialScaling = 0.8f;
    [SerializeField, Min(0f)] private float qRange = 2f;
    [SerializeField] private LayerMask enemyLayer = ~0;
    [SerializeField] private Transform hitPoint;

    private readonly System.Collections.Generic.HashSet<CombatHealth> castDamagedCombatTargets = new System.Collections.Generic.HashSet<CombatHealth>();
    private readonly System.Collections.Generic.HashSet<EnemyHealth> castDamagedLegacyTargets = new System.Collections.Generic.HashSet<EnemyHealth>();
    private float qLifestealTotalThisCast;

    private void Reset()
    {
        cooldown = 0.7f;
        duration = 0.42f;
        effectPower = 1.2f;
        animationName = "AKT2";
        debugLog = false;
        slashCount = 3;
        slashInterval = 0.15f;
        baseDamage = 20f;
        physicalScaling = 0.6f;
        specialScaling = 0.8f;
        qRange = 2f;
        enemyLayer = ~0;
    }

    private void OnValidate()
    {
        if (animationName == "ATK2")
        {
            animationName = "AKT2";
        }
    }

    private void Awake()
    {
        if (animationName == "ATK2")
        {
            animationName = "AKT2";
        }
    }

    protected override void OnCastStarted()
    {
        castDamagedCombatTargets.Clear();
        castDamagedLegacyTargets.Clear();
        qLifestealTotalThisCast = 0f;

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Start. animation={animationName}, slashes={slashCount}, interval={slashInterval:F2}", this);
        }

        if (Controller != null && Controller.IsVeilBarrierActive())
        {
            Debug.Log("[Player01 Q] cast while W active", this);
        }
    }

    protected override IEnumerator CastRoutine()
    {
        int count = Mathf.Max(1, slashCount);
        float interval = Mathf.Max(0f, slashInterval);
        float totalDuration = Mathf.Max(0f, duration);
        float lockDuration = Mathf.Max(totalDuration, interval * Mathf.Max(0, count - 1), 0.55f);

        for (int i = 0; i < count; i++)
        {
            PlaySlash(i + 1, count, lockDuration);

            if (i < count - 1)
            {
                if (interval > 0f)
                {
                    yield return new WaitForSeconds(interval);
                }
                else
                {
                    yield return null;
                }
            }
        }

        float remaining = totalDuration - Mathf.Max(0f, interval * (count - 1));
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        CompleteCast();
    }

    private void PlaySlash(int slashIndex, int slashTotal, float lockDuration)
    {
        if (Controller == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} skipped because Controller is null.", this);
            }

            return;
        }

        SkeletonAnimation spine = Controller.GetComponentInChildren<SkeletonAnimation>(true);
        if (spine == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} skipped because SkeletonAnimation is missing.", this);
            }

            return;
        }

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} try play {animationName} on {spine.name}.", this);
            Debug.Log($"[Q - QuickShear] Target: {Controller.GetSkeletonAnimationDebugSummary()}", this);
        }

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] TryPlayLockedSkillAnimation -> {animationName}.", this);
        }

        bool played = Controller.TryPlayLockedSkillAnimation(animationName, false, lockDuration, true, "Q");
        if (!played)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} failed to play '{animationName}'.", this);
            }

            return;
        }

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} requested '{animationName}' via shared controller entry.", this);
            Debug.Log($"[Q - QuickShear] damage={baseDamage:F2}, range={qRange:F2}", this);
        }

        float dealtDamage = ApplySlashDamage();
        if (dealtDamage > 0f)
        {
            ApplyQLifeSteal(dealtDamage);
        }

        Controller.StartCoroutine(LogTrackNextFrame(slashIndex, slashTotal));
    }

    private float ApplySlashDamage()
    {
        Vector3 origin = transform.position;
        Vector3 facing = Controller != null ? Controller.GetFacingWorldDirection() : transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = transform.forward;
            facing.y = 0f;
        }

        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = Vector3.forward;
        }

        facing.Normalize();
        Vector3 center = hitPoint != null ? hitPoint.position : origin + facing * (Mathf.Max(0.1f, qRange) * 0.5f);
        float finalDamage = ResolveDamage();
        Collider[] hits = Physics.OverlapSphere(center, Mathf.Max(0.1f, qRange), enemyLayer, QueryTriggerInteraction.Collide);
        float totalDamageDealt = 0f;

        foreach (Collider hit in hits)
        {
            if (!BattleTargetUtility.IsMonster(hit, transform))
            {
                continue;
            }

            if (!IsInFrontSlashArea(hit, origin, facing))
            {
                continue;
            }

            CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(hit, transform);
            if (combatHealth != null && castDamagedCombatTargets.Add(combatHealth))
            {
                float beforeHealth = ResolveCurrentHealth(combatHealth);
                combatHealth.TakeDamage(new BattleDamage(finalDamage, BattleDamageType.Physical, gameObject));
                float afterHealth = ResolveCurrentHealth(combatHealth);
                float actualDamage = Mathf.Max(0f, beforeHealth - afterHealth);
                totalDamageDealt += actualDamage;
                continue;
            }

            EnemyHealth legacyHealth = BattleTargetUtility.GetMonsterLegacyHealth(hit, transform);
            if (legacyHealth != null && castDamagedLegacyTargets.Add(legacyHealth))
            {
                int damageInt = Mathf.Max(1, Mathf.RoundToInt(finalDamage));
                int beforeHp = Mathf.Max(0, legacyHealth.hp);
                legacyHealth.TakeDamage(damageInt, gameObject);
                int actualDamage = Mathf.Clamp(beforeHp, 0, damageInt);
                totalDamageDealt += actualDamage;
            }
        }

        return totalDamageDealt;
    }

    private void ApplyQLifeSteal(float damageDealt)
    {
        float healAmount = Mathf.Max(0f, damageDealt);
        if (healAmount <= 0f)
        {
            return;
        }

        float beforeHealth = ResolvePlayerCurrentHealth();
        if (!TryHealPlayer(healAmount))
        {
            return;
        }

        float afterHealth = ResolvePlayerCurrentHealth();
        float actualHeal = Mathf.Max(0f, afterHealth - beforeHealth);
        if (actualHeal <= 0f)
        {
            return;
        }

        qLifestealTotalThisCast += actualHeal;
        Debug.Log($"[Player01 Q Lifesteal] damage={damageDealt:F2} heal={actualHeal:F2}", this);
    }

    private float ResolveCurrentHealth(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        if (combatHealth.resourceBank != null)
        {
            return combatHealth.resourceBank.currentHealth;
        }

        return combatHealth.currentHealth;
    }

    private float ResolvePlayerCurrentHealth()
    {
        CombatHealth combatHealth = ResolvePlayerCombatHealth();
        if (combatHealth != null)
        {
            return ResolveCurrentHealth(combatHealth);
        }

        BattleResourceBank bank = ResolvePlayerResourceBank();
        return bank != null ? bank.currentHealth : 0f;
    }

    private bool TryHealPlayer(float amount)
    {
        if (amount <= 0f)
        {
            return false;
        }

        CombatHealth combatHealth = ResolvePlayerCombatHealth();
        if (combatHealth != null)
        {
            float maxHealth = ResolvePlayerMaxHealth(combatHealth);
            if (maxHealth > 0f)
            {
                float before = ResolveCurrentHealth(combatHealth);
                float healed = Mathf.Min(amount, Mathf.Max(0f, maxHealth - before));
                if (healed > 0f)
                {
                    SetPlayerCurrentHealth(combatHealth, before + healed);
                }
            }
            return true;
        }

        BattleResourceBank bank = ResolvePlayerResourceBank();
        if (bank != null)
        {
            float before = Mathf.Max(0f, bank.currentHealth);
            float healed = Mathf.Min(amount, Mathf.Max(0f, bank.maxHealth - before));
            if (healed > 0f)
            {
                bank.currentHealth = before + healed;
            }
            return true;
        }

        return false;
    }

    private CombatHealth ResolvePlayerCombatHealth()
    {
        CombatHealth combatHealth = GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return combatHealth;
        }

        if (Controller != null)
        {
            combatHealth = Controller.GetComponent<CombatHealth>();
            if (combatHealth != null)
            {
                return combatHealth;
            }
        }

        return GetComponentInParent<CombatHealth>();
    }

    private BattleResourceBank ResolvePlayerResourceBank()
    {
        BattleResourceBank bank = GetComponent<BattleResourceBank>();
        if (bank != null)
        {
            return bank;
        }

        if (Controller != null)
        {
            bank = Controller.GetComponent<BattleResourceBank>();
            if (bank != null)
            {
                return bank;
            }
        }

        return GetComponentInParent<BattleResourceBank>();
    }

    private float ResolvePlayerMaxHealth(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        if (combatHealth.resourceBank != null)
        {
            return Mathf.Max(0f, combatHealth.resourceBank.maxHealth);
        }

        if (combatHealth.stats != null)
        {
            return Mathf.Max(0f, combatHealth.stats.maxHealth);
        }

        return Mathf.Max(0f, combatHealth.currentHealth);
    }

    private void SetPlayerCurrentHealth(CombatHealth combatHealth, float healthValue)
    {
        if (combatHealth == null)
        {
            return;
        }

        if (combatHealth.resourceBank != null)
        {
            float clamped = Mathf.Clamp(healthValue, 0f, combatHealth.resourceBank.maxHealth);
            combatHealth.resourceBank.currentHealth = clamped;
            combatHealth.currentHealth = clamped;
            return;
        }

        combatHealth.currentHealth = Mathf.Clamp(healthValue, 0f, ResolvePlayerMaxHealth(combatHealth));
    }

    private bool IsInFrontSlashArea(Collider hit, Vector3 origin, Vector3 facing)
    {
        Vector3 targetPoint = hit != null ? hit.bounds.center : origin;
        Vector3 toTarget = targetPoint - origin;
        toTarget.y = 0f;

        float range = Mathf.Max(0.1f, qRange);
        if (toTarget.sqrMagnitude > range * range)
        {
            return false;
        }

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        return Vector3.Dot(toTarget.normalized, facing) >= 0.15f;
    }

    private float ResolveDamage()
    {
        return PlayerSkillDamageUtility.CalculateHybridSkillDamage(
            this,
            gameObject,
            baseDamage,
            physicalScaling,
            specialScaling,
            "Player01 Q");
    }

    private IEnumerator LogTrackNextFrame(int slashIndex, int slashTotal)
    {
        yield return null;

        if (Controller == null)
        {
            yield break;
        }

        SkeletonAnimation spine = Controller.GetComponentInChildren<SkeletonAnimation>(true);
        if (spine == null || spine.AnimationState == null)
        {
            Debug.LogWarning($"[Q - QuickShear] Next frame track check failed after slash {slashIndex}/{slashTotal}: SkeletonAnimation missing.", this);
            yield break;
        }

        TrackEntry current = spine.AnimationState.GetCurrent(0);
        string currentName = current != null && current.Animation != null ? current.Animation.Name : "<none>";
        if (currentName == animationName)
        {
            Debug.Log($"[Q - QuickShear] Next frame Track0 is still {currentName}.", this);
        }
        else
        {
            Debug.LogWarning($"[Q - QuickShear] Next frame Track0 changed to {currentName}. currentLocomotion={Controller.GetCurrentLocomotionAnimationName()}, currentSkill={Controller.GetCurrentSkillName()}.", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "Q - QuickShear";
    }

    protected override void OnCastFinished()
    {
        if (qLifestealTotalThisCast > 0f)
        {
            Debug.Log($"[Player01 Q Lifesteal] totalHeal={qLifestealTotalThisCast:F2}", this);
        }
    }

    protected override int SkillIndex => 0;
}
