using System;
using System.Collections.Generic;
using UnityEngine;

public class CombatHealth : MonoBehaviour
{
    [Header("生命")]
    public CombatStats stats;
    public BattleResourceBank resourceBank;
    [Min(0f)] public float currentHealth = 3f;
    public bool destroyOnDeath = true;

    [Header("Animation")]
    public Animator animator;
    public string hitTrigger = "Hit";
    public string deathTrigger = "Die";
    [Min(0f)] public float destroyDelayAfterDeath = 0.65f;

    [Header("Damage Popup")]
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private DamagePopupFloatingText damagePopupPrefab;
    [SerializeField] private Color normalDamageColor = Color.white;
    [SerializeField] private Color physicalDamageColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color specialDamageColor = new Color(0.78f, 0.35f, 1f, 1f);
    [SerializeField] private Color criticalDamageColor = new Color(1f, 0.84f, 0.2f, 1f);
    [SerializeField] private Color missDamageColor = new Color(0.75f, 0.95f, 1f, 1f);
    [SerializeField] private Vector3 damagePopupOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector2 damagePopupRandomOffset = new Vector2(0.3f, 0.15f);

    public event Action<GameObject> Died;
    public event Action<float, GameObject> Damaged;
    public event Action<float, float> OnShieldChanged;

    private bool dead;
    private float localShield;
    private float localMaxShield;
    private readonly Dictionary<string, float> incomingDamageMultipliers = new Dictionary<string, float>();
    private RuneRuntimeState runeRuntimeState;
    private bool warnedMissingDamagePopupPrefab;
    private static DamagePopupFloatingText defaultDamagePopupPrefab;
    private static bool attemptedLoadDefaultDamagePopupPrefab;

    private float MaxHealth => stats != null ? stats.maxHealth : (resourceBank != null ? resourceBank.maxHealth : currentHealth);
    public float MaxHealthValue => MaxHealth;
    public bool IsDead => dead;

    public float ResolveConfiguredMaxHealth(float fallback = 100f)
    {
        if (stats != null && stats.maxHealth > 0f)
        {
            return stats.maxHealth;
        }

        if (resourceBank != null && resourceBank.maxHealth > 0f)
        {
            return resourceBank.maxHealth;
        }

        return currentHealth > 0f ? currentHealth : fallback;
    }

    public void SyncHealthFromStats(bool refillCurrentHealth)
    {
        float previousCurrentHealth = currentHealth;
        float previousMaxHealth = resourceBank != null ? resourceBank.maxHealth : ResolveConfiguredMaxHealth();
        bool hadMatchingSerializedHealth = Mathf.Approximately(previousCurrentHealth, previousMaxHealth);

        if (resourceBank != null)
        {
            resourceBank.SyncHealthFromCombatStats(refillCurrentHealth);
            currentHealth = Mathf.Clamp(resourceBank.currentHealth, 0f, Mathf.Max(0f, resourceBank.maxHealth));
            return;
        }

        float resolvedMaxHealth = ResolveConfiguredMaxHealth(previousMaxHealth);
        if (refillCurrentHealth || hadMatchingSerializedHealth || currentHealth <= 0f)
        {
            currentHealth = Mathf.Max(0f, resolvedMaxHealth);
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, Mathf.Max(0f, resolvedMaxHealth));
        }
    }

    private void Awake()
    {
        if (stats == null)
        {
            stats = GetComponent<CombatStats>();
        }

        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        if (runeRuntimeState == null)
        {
            runeRuntimeState = GetComponent<RuneRuntimeState>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        DissolveOnDeath dissolveOnDeath = GetComponent<DissolveOnDeath>();
        if (dissolveOnDeath == null)
        {
            dissolveOnDeath = gameObject.AddComponent<DissolveOnDeath>();
        }

        dissolveOnDeath.EnsureHealthBindings();

        if (resourceBank != null)
        {
            resourceBank.OnShieldChanged += HandleResourceBankOnShieldChanged;
        }

        SyncHealthFromStats(refillCurrentHealth: false);
        localShield = Mathf.Max(0f, localShield);
        localMaxShield = Mathf.Max(0f, localMaxShield);
    }

    private void OnDestroy()
    {
        if (resourceBank != null)
        {
            resourceBank.OnShieldChanged -= HandleResourceBankOnShieldChanged;
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(new BattleDamage(amount, BattleDamageType.Physical, null));
    }

    public void TakeDamage(BattleDamage damage)
    {
        if (dead)
        {
            return;
        }

        if (ShouldIgnoreDamageFrom(damage.source))
        {
            return;
        }

        if (TryEvadeDamage(damage.source, out _))
        {
            ShowMissPopup();
            return;
        }

        Player01SkillController player1 = GetComponent<Player01SkillController>();
        if (player1 != null && player1.ShouldIgnoreIncomingDamage(damage))
        {
            return;
        }

        float outgoingDamage = BattleStatUtility.ApplyPlayerMoveSpeedDamageBonus(damage.source, damage.amount);
        damage.amount = outgoingDamage;
        float finalDamage = stats != null ? stats.ReduceDamage(damage) : outgoingDamage;
        finalDamage *= GetIncomingDamageMultiplier();
        finalDamage = AbsorbShieldDamage(finalDamage);
        Player2PrototypeController player2 = GetComponent<Player2PrototypeController>();
        if (player2 != null)
        {
            finalDamage = player2.ProcessIncomingDamageWithWGuard(finalDamage, damage);
        }

        if (resourceBank != null)
        {
            resourceBank.currentHealth = Mathf.Max(0f, resourceBank.currentHealth - finalDamage);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        }

        if (finalDamage > 0f)
        {
            if (BattleTargetUtility.IsPlayer(gameObject) && damage.source != null && BattleTargetUtility.IsMonster(damage.source))
            {
                runeRuntimeState?.NotifyIncomingMonsterDamage(damage.source, finalDamage);
            }

            Damaged?.Invoke(finalDamage, damage.source);
            ShowDamagePopup(finalDamage, ResolvePopupType(damage.damageType), damage.isCritical);
            TriggerAnimation(hitTrigger);
        }

        if (currentHealth <= 0f)
        {
            Die(damage.source);
        }
    }

    public void ApplyDirectDamage(float amount, GameObject source)
    {
        ApplyDirectDamage(amount, source, DamagePopupType.Normal, false);
    }

    public void ApplyDirectDamage(float amount, GameObject source, DamagePopupType popupType, bool isCritical = false)
    {
        if (dead)
        {
            return;
        }

        if (ShouldIgnoreDamageFrom(source))
        {
            return;
        }

        if (TryEvadeDamage(source, out _))
        {
            ShowMissPopup();
            return;
        }

        float finalDamage = BattleStatUtility.ApplyPlayerMoveSpeedDamageBonus(source, amount);
        finalDamage *= GetIncomingDamageMultiplier();
        finalDamage = AbsorbShieldDamage(finalDamage);

        if (resourceBank != null)
        {
            resourceBank.currentHealth = Mathf.Max(0f, resourceBank.currentHealth - finalDamage);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        }

        if (finalDamage > 0f)
        {
            if (BattleTargetUtility.IsPlayer(gameObject) && source != null && BattleTargetUtility.IsMonster(source))
            {
                runeRuntimeState?.NotifyIncomingMonsterDamage(source, finalDamage);
            }

            Damaged?.Invoke(finalDamage, source);
            ShowDamagePopup(finalDamage, popupType, isCritical);
            TriggerAnimation(hitTrigger);
        }

        if (currentHealth <= 0f)
        {
            Die(source);
        }
    }

    private bool ShouldIgnoreDamageFrom(GameObject source)
    {
        if (source == null)
        {
            return false;
        }

        bool sourceIsPlayer = BattleTargetUtility.IsPlayer(source);
        bool sourceIsMonster = BattleTargetUtility.IsMonster(source);
        bool targetIsPlayer = BattleTargetUtility.IsPlayer(gameObject);
        bool targetIsMonster = BattleTargetUtility.IsMonster(gameObject);

        if (sourceIsPlayer)
        {
            return !targetIsMonster;
        }

        if (sourceIsMonster)
        {
            return !targetIsPlayer;
        }

        return false;
    }

    public void Heal(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (resourceBank != null)
        {
            resourceBank.Heal(amount);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        }
    }

    public void SetShield(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (resourceBank != null)
        {
            resourceBank.SetShield(amount);
            return;
        }

        localShield = amount;
        localMaxShield = amount;
        OnShieldChanged?.Invoke(localShield, localMaxShield);
    }

    public void ClearShield()
    {
        if (resourceBank != null)
        {
            resourceBank.ClearShield();
            return;
        }

        localShield = 0f;
        localMaxShield = 0f;
        OnShieldChanged?.Invoke(localShield, localMaxShield);
    }

    public float GetShield()
    {
        return resourceBank != null ? resourceBank.CurrentShield : localShield;
    }

    public float GetMaxShield()
    {
        if (resourceBank != null)
        {
            return resourceBank.MaxShield;
        }

        return localMaxShield;
    }

    public bool HasActiveShield()
    {
        return GetShield() > 0f;
    }

    public float CurrentShield => GetShield();
    public float MaxShield => GetMaxShield();
    public bool HasShield => HasActiveShield();

    public float GetCurrentShield()
    {
        return GetShield();
    }

    public void AddDamageReductionModifier(string key, float multiplier)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        incomingDamageMultipliers[key] = Mathf.Max(0f, multiplier);
    }

    public void RemoveDamageReductionModifier(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        incomingDamageMultipliers.Remove(key);
    }

    public void SetIncomingDamageMultiplier(object source, float multiplier)
    {
        AddDamageReductionModifier(GetModifierKey(source), multiplier);
    }

    public void RemoveIncomingDamageMultiplier(object source)
    {
        RemoveDamageReductionModifier(GetModifierKey(source));
    }

    private float AbsorbShieldDamage(float amount)
    {
        amount = Mathf.Max(0f, amount);
        float shieldUsed = Mathf.Min(GetShield(), amount);
        if (shieldUsed <= 0f)
        {
            return amount;
        }

        float remainingShield = GetShield() - shieldUsed;
        if (resourceBank != null)
        {
            resourceBank.SetShieldCurrent(remainingShield);
        }
        else
        {
            localShield = remainingShield;
            localMaxShield = Mathf.Max(localMaxShield, localShield);
            OnShieldChanged?.Invoke(localShield, localMaxShield);
        }

        return amount - shieldUsed;
    }

    private float GetIncomingDamageMultiplier()
    {
        float multiplier = 1f;
        foreach (float value in incomingDamageMultipliers.Values)
        {
            multiplier *= Mathf.Max(0f, value);
        }

        return multiplier;
    }

    private static string GetModifierKey(object source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        return source is string stringKey ? stringKey : source.GetHashCode().ToString();
    }

    private void HandleResourceBankOnShieldChanged(float currentShield, float maxShield)
    {
        OnShieldChanged?.Invoke(currentShield, maxShield);
    }

    private DamagePopupType ResolvePopupType(BattleDamageType damageType)
    {
        return damageType == BattleDamageType.Special ? DamagePopupType.Special : DamagePopupType.Physical;
    }

    private bool TryEvadeDamage(GameObject source, out float finalEvasionChance)
    {
        finalEvasionChance = 0f;
        CombatStats defenderStats = stats;
        CombatStats attackerStats = BattleStatUtility.GetCombatStats(source);
        BattleStatUtility.ResolveFinalEvasionAndHitChance(
            gameObject,
            source,
            out float rawEvasionChance,
            out float clampedEvasionChance,
            out finalEvasionChance,
            out float finalHitChance);
        float randomRoll = UnityEngine.Random.value;
        bool evaded = finalEvasionChance > 0f && randomRoll < finalEvasionChance;
        Debug.Log(
            $"[CombatEvasion] attacker={(source != null ? source.name : "null")} attackerRank={BattleStatUtility.GetAttackerRankLabel(source)} defender={name} defenderSpeed={(defenderStats != null ? defenderStats.speed : 0f):F2} defenderLuck={(defenderStats != null ? defenderStats.luck : 0f):F2} attackerSpeed={(attackerStats != null ? attackerStats.speed : 0f):F2} rawEvasionChance={rawEvasionChance:F4} clampedEvasionChance={clampedEvasionChance:F4} accuracyMultiplier={BattleStatUtility.GetAccuracyMultiplier(attackerStats):F2} finalEvasionChance={finalEvasionChance:F4} finalHitChance={finalHitChance:F4} randomRoll={randomRoll:F4} result={(evaded ? "Miss" : "Hit")}",
            this);

        if (!evaded)
        {
            return false;
        }

        if (BattleTargetUtility.IsPlayer(gameObject) && BattleTargetUtility.IsMonster(source))
        {
            Debug.Log($"[EnemyAttack] Evaded target={name} attacker={source.name}", this);
        }

        return true;
    }

    private void ShowDamagePopup(float damage, DamagePopupType popupType, bool isCritical)
    {
        if (!showDamageNumbers || damage <= 0f)
        {
            return;
        }

        Vector3 worldPosition = transform.position + damagePopupOffset;
        worldPosition.x += UnityEngine.Random.Range(-damagePopupRandomOffset.x, damagePopupRandomOffset.x);
        worldPosition.y += UnityEngine.Random.Range(-damagePopupRandomOffset.y, damagePopupRandomOffset.y);
        worldPosition.z += UnityEngine.Random.Range(-damagePopupRandomOffset.x, damagePopupRandomOffset.x);

        string message = Mathf.RoundToInt(damage).ToString();

        Color color = ResolveDamagePopupColor(popupType, isCritical);
        DamagePopupFloatingText popupPrefab = ResolveDamagePopupPrefab();
        if (popupPrefab != null)
        {
            DamagePopupFloatingText popup = Instantiate(popupPrefab, worldPosition, Quaternion.identity);
            popup.Show(message, color);
        }
        else
        {
            if (!warnedMissingDamagePopupPrefab)
            {
                warnedMissingDamagePopupPrefab = true;
                Debug.LogWarning("[CombatHealth] damagePopupPrefab is not assigned and no default prefab was found at Resources/Prefabs/UI/DamagePopupFloatingText. Using runtime fallback popup.", this);
            }

            DamagePopupFloatingText.SpawnFallback(message, worldPosition, color);
        }
    }

    private void ShowMissPopup()
    {
        if (!showDamageNumbers)
        {
            return;
        }

        Vector3 worldPosition = transform.position + damagePopupOffset;
        worldPosition.x += UnityEngine.Random.Range(-damagePopupRandomOffset.x, damagePopupRandomOffset.x);
        worldPosition.y += UnityEngine.Random.Range(-damagePopupRandomOffset.y, damagePopupRandomOffset.y);
        worldPosition.z += UnityEngine.Random.Range(-damagePopupRandomOffset.x, damagePopupRandomOffset.x);

        DamagePopupFloatingText popupPrefab = ResolveDamagePopupPrefab();
        if (popupPrefab != null)
        {
            DamagePopupFloatingText popup = Instantiate(popupPrefab, worldPosition, Quaternion.identity);
            popup.Show("miss", missDamageColor);
        }
        else
        {
            DamagePopupFloatingText.SpawnFallback("miss", worldPosition, missDamageColor);
        }
    }

    private DamagePopupFloatingText ResolveDamagePopupPrefab()
    {
        if (damagePopupPrefab != null)
        {
            return damagePopupPrefab;
        }

        if (!attemptedLoadDefaultDamagePopupPrefab)
        {
            attemptedLoadDefaultDamagePopupPrefab = true;
            defaultDamagePopupPrefab = Resources.Load<DamagePopupFloatingText>("Prefabs/UI/DamagePopupFloatingText");
        }

        return defaultDamagePopupPrefab;
    }

    private Color ResolveDamagePopupColor(DamagePopupType popupType, bool isCritical)
    {
        if (isCritical)
        {
            return criticalDamageColor;
        }

        return normalDamageColor;
    }

    private void Die(GameObject killer)
    {
        if (dead)
        {
            return;
        }

        dead = true;
        Died?.Invoke(killer);
        TriggerAnimation(deathTrigger);

        if (destroyOnDeath)
        {
            if (GetComponent<DissolveOnDeath>() != null)
            {
                Debug.Log($"[DeathFlow] Skip immediate destroy because DissolveOnDeath exists owner={name}", this);
            }
            else
            {
                Destroy(gameObject, destroyDelayAfterDeath);
            }
        }
    }

    private void TriggerAnimation(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        animator.SetTrigger(triggerName);
    }
}
