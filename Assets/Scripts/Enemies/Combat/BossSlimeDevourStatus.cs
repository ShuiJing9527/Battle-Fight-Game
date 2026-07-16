using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossSlimeDevourStatus : MonoBehaviour
{
    private readonly List<SpriteColorBinding> spriteBindings = new List<SpriteColorBinding>();
    private readonly List<MaterialColorBinding> materialBindings = new List<MaterialColorBinding>();

    private Coroutine activeRoutine;
    private CombatHealth combatHealth;
    private Rigidbody targetBody;
    private EnemyController ownerController;
    private int ownerSequenceId;
    private GameObject currentDamageSource;
    private float nextTraceTime;
    private float activeDevourStartTime;
    private BossSlimeDevourSkill.RuntimeConfig runtimeConfig;
    private int totalDamageTicksApplied;
    private float totalDamageApplied;

    private struct SpriteColorBinding
    {
        public SpriteRenderer renderer;
        public Color originalColor;
    }

    private struct MaterialColorBinding
    {
        public Renderer renderer;
        public Material material;
        public string propertyName;
        public Color originalColor;
    }

    public static BossSlimeDevourStatus ResolveOrAdd(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        BossSlimeDevourStatus status = target.GetComponent<BossSlimeDevourStatus>();
        if (status == null)
        {
            status = target.AddComponent<BossSlimeDevourStatus>();
        }

        return status;
    }

    public void Apply(
        GameObject damageSource,
        Transform holdAnchor,
        EnemyController actionOwner,
        int actionSequenceId,
        BossSlimeDevourSkill.RuntimeConfig config)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            RestoreVisuals();
        }

        CacheRuntimeReferences();
        CacheVisuals();
        runtimeConfig = config;
        activeDevourStartTime = Time.time;
        ApplyDarkTint(runtimeConfig.DarkTint);
        currentDamageSource = damageSource;
        ownerController = actionOwner;
        ownerSequenceId = actionSequenceId;
        activeRoutine = StartCoroutine(DevourRoutine(damageSource, holdAnchor, runtimeConfig));
    }

    private IEnumerator DevourRoutine(
        GameObject damageSource,
        Transform holdAnchor,
        BossSlimeDevourSkill.RuntimeConfig config)
    {
        float devourStartTime = Time.time;
        activeDevourStartTime = devourStartTime;
        float safeDuration = Mathf.Max(0.1f, config.TotalDuration);
        float safeTickInterval = Mathf.Max(0.1f, config.DamageTickInterval);
        float nextDamageTickTime = devourStartTime + safeTickInterval;
        int damageTickIndex = 0;
        const int maxTicksPerFrame = 3;

        totalDamageTicksApplied = 0;
        totalDamageApplied = 0f;

        if (combatHealth != null && !combatHealth.IsDead)
        {
            if (config.DealInitialDamage && config.InitialDamage > 0f)
            {
                float valuePassedToTakeDamage = ResolveDamageValuePassedToHealth(
                    combatHealth,
                    BattleDamageType.Special,
                    config.InitialDamage,
                    out float targetDefense);
                float healthBefore = ResolveCombatHealthValue(combatHealth);
                float shieldBefore = combatHealth.GetShield();
                Debug.Log(
                    "[BossDevourDamageTrace] " +
                    "event=InitialDamageApplying " +
                    "target=" + combatHealth.name +
                    " configuredDamage=" + config.InitialDamage.ToString("F2") +
                    " damagePassedToHealth=" + valuePassedToTakeDamage.ToString("F2") +
                    " targetDefense=" + targetDefense.ToString("F2") +
                    " sequenceId=" + ownerSequenceId,
                    this);
                combatHealth.TakeDamage(new BattleDamage(valuePassedToTakeDamage, BattleDamageType.Special, damageSource));
                float healthAfter = ResolveCombatHealthValue(combatHealth);
                float shieldAfter = combatHealth.GetShield();
                float actualHealthLoss = Mathf.Max(0f, healthBefore - healthAfter);
                float actualShieldLoss = Mathf.Max(0f, shieldBefore - shieldAfter);
                Debug.Log(
                    "[BossDevourDamageTrace] " +
                    "event=InitialDamageApplied " +
                    "target=" + combatHealth.name +
                    " sequenceId=" + ownerSequenceId +
                    " healthBefore=" + healthBefore.ToString("F2") +
                    " healthAfter=" + healthAfter.ToString("F2") +
                    " shieldBefore=" + shieldBefore.ToString("F2") +
                    " shieldAfter=" + shieldAfter.ToString("F2") +
                    " actualHpLoss=" + actualHealthLoss.ToString("F2") +
                    " actualShieldLoss=" + actualShieldLoss.ToString("F2"),
                    this);
            }
            else
            {
                Debug.Log(
                    "[BossDevourDamageTrace] " +
                    "event=InitialDamageSkipped " +
                    "reason=" + (!config.DealInitialDamage ? "Disabled" : "ZeroDamage") +
                    " target=" + combatHealth.name +
                    " sequenceId=" + ownerSequenceId,
                    this);
            }

            if (config.DealDamageWhileHolding)
            {
                Debug.Log(
                    "[BossDevourDamageTrace] " +
                    "event=TickDamageSequenceStarted " +
                    "sequenceId=" + ownerSequenceId +
                    " target=" + combatHealth.name +
                    " startingTickDamage=" + config.StartingTickDamage.ToString("F2") +
                    " damageIncreasePerTick=" + config.DamageIncreasePerTick.ToString("F2") +
                    " tickInterval=" + safeTickInterval.ToString("F2") +
                    " maximumTickDamage=" + config.MaximumTickDamage.ToString("F2"),
                    this);
            }
        }
        else
        {
            Debug.Log(
                "[BossDevourDamageTrace] " +
                "event=InitialDamageSkipped " +
                "reason=InvalidTarget " +
                "target=" + (combatHealth != null ? combatHealth.name : "null") +
                " sequenceId=" + ownerSequenceId,
                this);
        }

        while (true)
        {
            float elapsed = Time.time - devourStartTime;
            if (combatHealth == null || combatHealth.IsDead)
            {
                LogTickDamageSequenceStopped("TargetDead");
                break;
            }

            if (ownerController == null || !ownerController.IsBossDevourActionActive(ownerSequenceId))
            {
                LogTickDamageSequenceStopped(ownerController == null ? "Interrupted" : "SequenceExpired");
                Debug.LogWarning(
                    "[BossActionLockTrace] event=InvalidAttractionState " +
                    "activeKind=" + (ownerController != null ? ownerController.CurrentBossAttackKindName : "None") +
                    " target=" + name +
                    " sequenceId=" + ownerSequenceId,
                    this);
                break;
            }

            if (Time.time >= nextTraceTime)
            {
                nextTraceTime = Time.time + 0.5f;
                Debug.Log(
                    "[BossActionLockTrace] event=AttractionActive " +
                    "activeKind=Devour target=" + name +
                    " sequenceId=" + ownerSequenceId,
                    this);
            }

            HoldInsideBossBody(holdAnchor, config, elapsed);

            if (config.DealDamageWhileHolding &&
                (config.StartingTickDamage > 0f || config.DamageIncreasePerTick > 0f))
            {
                int ticksProcessedThisFrame = 0;
                while (Time.time >= nextDamageTickTime &&
                       ticksProcessedThisFrame < maxTicksPerFrame)
                {
                    float configuredTickDamage = Mathf.Min(
                        config.StartingTickDamage + damageTickIndex * config.DamageIncreasePerTick,
                        config.MaximumTickDamage);
                    float valuePassedToTakeDamage = ResolveDamageValuePassedToHealth(
                        combatHealth,
                        BattleDamageType.Special,
                        configuredTickDamage,
                        out float targetDefense);

                    Debug.Log(
                        "[BossDevourDamageTrace] " +
                        "event=TickDamageApplying " +
                        "sequenceId=" + ownerSequenceId +
                        " target=" + combatHealth.name +
                        " tickIndex=" + damageTickIndex +
                        " calculatedDamage=" + configuredTickDamage.ToString("F2") +
                        " damagePassedToHealth=" + valuePassedToTakeDamage.ToString("F2") +
                        " targetDefense=" + targetDefense.ToString("F2") +
                        " elapsedDevourTime=" + elapsed.ToString("F2"),
                        this);

                    float healthBefore = ResolveCombatHealthValue(combatHealth);
                    float shieldBefore = combatHealth.GetShield();
                    combatHealth.TakeDamage(new BattleDamage(valuePassedToTakeDamage, BattleDamageType.Special, damageSource));
                    float healthAfter = ResolveCombatHealthValue(combatHealth);
                    float shieldAfter = combatHealth.GetShield();
                    float actualHealthLoss = Mathf.Max(0f, healthBefore - healthAfter);
                    float actualShieldLoss = Mathf.Max(0f, shieldBefore - shieldAfter);
                    float actualDurabilityLoss = actualHealthLoss + actualShieldLoss;

                    totalDamageTicksApplied++;
                    totalDamageApplied += actualDurabilityLoss;

                    Debug.Log(
                        "[BossDevourDamageTrace] " +
                        "event=TickDamageApplied " +
                        "sequenceId=" + ownerSequenceId +
                        " target=" + combatHealth.name +
                        " tickIndex=" + damageTickIndex +
                        " requestedDamage=" + configuredTickDamage.ToString("F2") +
                        " damagePassedToHealth=" + valuePassedToTakeDamage.ToString("F2") +
                        " healthBefore=" + healthBefore.ToString("F2") +
                        " healthAfter=" + healthAfter.ToString("F2") +
                        " shieldBefore=" + shieldBefore.ToString("F2") +
                        " shieldAfter=" + shieldAfter.ToString("F2") +
                        " actualHpLoss=" + actualHealthLoss.ToString("F2") +
                        " actualShieldLoss=" + actualShieldLoss.ToString("F2") +
                        " actualDurabilityLoss=" + actualDurabilityLoss.ToString("F2"),
                        this);

                    damageTickIndex++;
                    nextDamageTickTime += safeTickInterval;
                    ticksProcessedThisFrame++;

                    if (combatHealth == null || combatHealth.IsDead)
                    {
                        LogTickDamageSequenceStopped("TargetDead");
                        break;
                    }
                }
            }

            if (elapsed >= safeDuration)
            {
                LogTickDamageSequenceStopped("Completed");
                break;
            }

            yield return null;
        }

        RestoreVisuals();
        activeRoutine = null;
        ownerController = null;
        ownerSequenceId = 0;
        currentDamageSource = null;
        activeDevourStartTime = 0f;
        runtimeConfig = default;
        totalDamageTicksApplied = 0;
        totalDamageApplied = 0f;
    }

    public void ForceStop(string reason)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        LogTickDamageSequenceStopped(reason);
        RestoreVisuals();
        ownerController = null;
        ownerSequenceId = 0;
        currentDamageSource = null;
        activeDevourStartTime = 0f;
        runtimeConfig = default;
        totalDamageTicksApplied = 0;
        totalDamageApplied = 0f;
        Debug.Log(
            "[BossActionLockTrace] event=ActionLockReleased kind=Devour sequenceId=0 endReason=" + reason,
            this);
    }

    private void CacheRuntimeReferences()
    {
        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
            if (combatHealth == null)
            {
                combatHealth = GetComponentInParent<CombatHealth>();
            }
            if (combatHealth == null)
            {
                combatHealth = GetComponentInChildren<CombatHealth>(true);
            }
        }

        if (targetBody == null)
        {
            targetBody = GetComponent<Rigidbody>();
            if (targetBody == null)
            {
                targetBody = GetComponentInParent<Rigidbody>();
            }
        }
    }

    private void CacheVisuals()
    {
        spriteBindings.Clear();
        materialBindings.Clear();

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteBindings.Add(new SpriteColorBinding
            {
                renderer = spriteRenderer,
                originalColor = spriteRenderer.color
            });
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is SpriteRenderer || renderer.sharedMaterial == null)
            {
                continue;
            }

            Material material = renderer.material;
            string propertyName = ResolveColorPropertyName(material);
            if (string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            materialBindings.Add(new MaterialColorBinding
            {
                renderer = renderer,
                material = material,
                propertyName = propertyName,
                originalColor = material.GetColor(propertyName)
            });
        }
    }

    private void ApplyDarkTint(Color darkTint)
    {
        for (int i = 0; i < spriteBindings.Count; i++)
        {
            SpriteColorBinding binding = spriteBindings[i];
            if (binding.renderer == null)
            {
                continue;
            }

            Color color = binding.originalColor;
            binding.renderer.color = new Color(color.r * darkTint.r, color.g * darkTint.g, color.b * darkTint.b, color.a);
        }

        for (int i = 0; i < materialBindings.Count; i++)
        {
            MaterialColorBinding binding = materialBindings[i];
            if (binding.material == null || string.IsNullOrEmpty(binding.propertyName))
            {
                continue;
            }

            Color color = binding.originalColor;
            binding.material.SetColor(binding.propertyName, new Color(color.r * darkTint.r, color.g * darkTint.g, color.b * darkTint.b, color.a));
        }
    }

    private void HoldInsideBossBody(Transform holdAnchor, BossSlimeDevourSkill.RuntimeConfig config, float elapsed)
    {
        if (holdAnchor == null)
        {
            return;
        }

        Vector3 targetPosition = ownerController != null
            ? ownerController.ResolveBossDevourHoldTargetPosition(holdAnchor, config.HoldOffset, transform)
            : holdAnchor.position + config.HoldOffset;

        bool inPullPhase = elapsed < config.PullDuration;
        float phaseStrength = inPullPhase
            ? config.PullStrength + config.PullAcceleration * Mathf.Clamp01(config.PullDuration <= 0f ? 1f : elapsed / config.PullDuration)
            : config.HoldPositionStrength;
        float interpolationFactor = Mathf.Clamp01(Time.deltaTime * Mathf.Max(0f, phaseStrength));
        float baseStepLimit = Mathf.Max(0f, config.PullSpeed) * Time.deltaTime;
        float horizontalStepLimit = Mathf.Max(baseStepLimit, Mathf.Max(0f, config.MaximumHorizontalPullSpeed) * Time.deltaTime);
        float verticalStepLimit = Mathf.Max(baseStepLimit, Mathf.Max(0f, config.MaximumVerticalPullSpeed) * Time.deltaTime);
        float stopDistance = inPullPhase
            ? Mathf.Max(0f, config.PullStopDistance)
            : Mathf.Max(0f, config.HoldStopDistance);

        Vector3 currentPosition = transform.position;
        Vector3 desiredPosition = Vector3.Lerp(currentPosition, targetPosition, interpolationFactor);
        Vector3 desiredDelta = desiredPosition - currentPosition;
        Vector3 horizontalDelta = Vector3.ProjectOnPlane(desiredDelta, Vector3.up);
        if (horizontalStepLimit > 0f)
        {
            horizontalDelta = Vector3.ClampMagnitude(horizontalDelta, horizontalStepLimit);
        }

        float verticalDeltaY = desiredDelta.y;
        if (verticalStepLimit > 0f)
        {
            verticalDeltaY = Mathf.Clamp(verticalDeltaY, -verticalStepLimit, verticalStepLimit);
        }

        Vector3 nextPosition = currentPosition + horizontalDelta + Vector3.up * verticalDeltaY;
        if ((targetPosition - nextPosition).sqrMagnitude <= stopDistance * stopDistance)
        {
            nextPosition = targetPosition;
        }

        if (targetBody != null)
        {
            targetBody.linearVelocity = Vector3.zero;
            targetBody.angularVelocity = Vector3.zero;
            targetBody.position = nextPosition;
        }

        transform.position = nextPosition;
    }

    private static float ResolveCombatHealthValue(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return health.currentHealth;
    }

    private static float ResolveDamageValuePassedToHealth(
        CombatHealth health,
        BattleDamageType damageType,
        float configuredDamage,
        out float targetDefense)
    {
        targetDefense = 0f;
        if (health == null)
        {
            return configuredDamage;
        }

        CombatStats stats = health.stats != null ? health.stats : health.GetComponent<CombatStats>();
        if (stats != null)
        {
            targetDefense = damageType == BattleDamageType.Physical
                ? Mathf.Max(0f, stats.physicalDefense)
                : Mathf.Max(0f, stats.specialDefense);
        }

        return Mathf.Max(0f, configuredDamage + targetDefense);
    }

    private void LogTickDamageSequenceStopped(string reason)
    {
        if (ownerSequenceId <= 0)
        {
            return;
        }

        Debug.Log(
            "[BossDevourDamageTrace] " +
            "event=TickDamageSequenceStopped " +
            "sequenceId=" + ownerSequenceId +
            " reason=" + reason +
            " elapsedTime=" + Mathf.Max(0f, Time.time - activeDevourStartTime).ToString("F2") +
            " configuredTotalDuration=" + runtimeConfig.TotalDuration.ToString("F2") +
            " totalTicks=" + totalDamageTicksApplied +
            " totalDamage=" + totalDamageApplied.ToString("F2") +
            " ownerActionActive=" + (ownerController != null && ownerController.IsBossDevourActionActive(ownerSequenceId)) +
            " sequenceValid=" + (ownerSequenceId > 0),
            this);
    }

    private void RestoreVisuals()
    {
        for (int i = 0; i < spriteBindings.Count; i++)
        {
            SpriteColorBinding binding = spriteBindings[i];
            if (binding.renderer != null)
            {
                binding.renderer.color = binding.originalColor;
            }
        }

        for (int i = 0; i < materialBindings.Count; i++)
        {
            MaterialColorBinding binding = materialBindings[i];
            if (binding.material != null && !string.IsNullOrEmpty(binding.propertyName))
            {
                binding.material.SetColor(binding.propertyName, binding.originalColor);
            }
        }

        spriteBindings.Clear();
        materialBindings.Clear();
    }

    private void OnDisable()
    {
        RestoreVisuals();
    }

    private void OnDestroy()
    {
        RestoreVisuals();
    }

    private static string ResolveColorPropertyName(Material material)
    {
        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_BaseColor"))
        {
            return "_BaseColor";
        }

        if (material.HasProperty("_Color"))
        {
            return "_Color";
        }

        return null;
    }
}
