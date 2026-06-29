using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

public class Player1Skill_E_BrokenDash : Player01SkillBase
{
    [Header("E - 灵体疾行 / 核心参数")]
    [FormerlySerializedAs("cooldown")]
    [SerializeField, Min(0f)] private float eCooldown = 10f;
    [FormerlySerializedAs("duration")]
    [SerializeField, Min(0f)] private float eDuration = 4f;
    [SerializeField, Min(0f)] private float eManaCost = 30f;

    [Header("E - 灵体疾行 / 移动")]
    [FormerlySerializedAs("speedMultiplier")]
    [SerializeField, Min(0f)] private float eMoveSpeedMultiplier = 1.6f;

    [Header("E - 灵体疾行 / 回复")]
    [SerializeField, Min(0f)] private float eHealPerTick = 5f;
    [SerializeField, Min(0.01f)] private float eHealTickInterval = 0.5f;

    [Header("E - 灵体疾行 / 灵体状态")]
    [FormerlySerializedAs("ignoreObstacleCollision")]
    [SerializeField] private bool eIgnoreTerrainCollision = true;
    [SerializeField] private bool eIgnoreEnemyCollision = true;
    [SerializeField] private bool eImmuneToMonsterPhysicalDamage = true;
    [FormerlySerializedAs("obstacleLayers")]
    [SerializeField] private LayerMask terrainCollisionLayers = 1 << 3;
    [SerializeField] private LayerMask enemyCollisionLayers = ~0;

    [Header("E - Ghost Visual")]
    [SerializeField] private Player01GhostStateVisual eGhostStateVisual;
    [SerializeField] private bool eEnableGhostStateVisual = true;

    [Header("E - Shadow Follower")]
    [SerializeField] private Player01EGhostShadowFollower eGhostShadowFollower;
    [SerializeField] private bool eEnableGhostShadowFollower = true;

    [Header("E - Ghost Particles")]
    [SerializeField] private Player01EGhostParticleController eGhostParticleController;
    [SerializeField] private bool eEnableGhostParticles = true;

    public bool IsRunningBoost { get; private set; }

    private PlayerMovement cachedMovement;
    private float cachedOriginalMoveSpeed = -1f;
    private readonly Dictionary<int, bool> cachedTerrainCollisionStates = new Dictionary<int, bool>();
    private readonly Dictionary<int, bool> cachedEnemyCollisionStates = new Dictionary<int, bool>();
    private CombatHealth cachedCombatHealth;

    private void Reset()
    {
        cooldown = 2.2f;
        duration = 3f;
        effectPower = 4f;
        animationName = "Run";
        debugLog = true;
        eCooldown = 10f;
        eDuration = 4f;
        eManaCost = 30f;
        eMoveSpeedMultiplier = 1.6f;
        eHealPerTick = 5f;
        eHealTickInterval = 0.5f;
        eIgnoreTerrainCollision = true;
        eIgnoreEnemyCollision = true;
        eImmuneToMonsterPhysicalDamage = true;
        terrainCollisionLayers = 1 << 3;
        enemyCollisionLayers = ~0;
        SyncEStateConfig();
    }

    public override bool Cast()
    {
        if (IsRunningBoost)
        {
            if (debugLog)
            {
                Debug.Log("[Player01 E Run] already running, ignored.", this);
            }

            return false;
        }

        return base.Cast();
    }

    private void Awake()
    {
        SyncEStateConfig();
        cachedMovement = GetComponent<PlayerMovement>();
        cachedCombatHealth = GetComponent<CombatHealth>();
        CacheGhostStateVisual();
        CacheGhostShadowFollower();
        CacheGhostParticleController();

        if (debugLog)
        {
            Debug.Log(eGhostShadowFollower != null
                ? $"[E Shadow] ghostShadowFollower found: {eGhostShadowFollower.name}"
                : "[E Shadow] ghostShadowFollower is null", this);

            Debug.Log(eGhostParticleController != null
                ? $"[E GhostParticles] controller found: {eGhostParticleController.name}"
                : "[E GhostParticles] controller is null", this);
        }
    }

    public override void Initialize(Player01SkillController controller)
    {
        base.Initialize(controller);
        SyncEStateConfig();
    }

    private void OnValidate()
    {
        SyncEStateConfig();
    }

    protected override bool ShouldLoopAnimation()
    {
        return true;
    }

    protected override void OnCastStarted()
    {
        IsRunningBoost = true;
        SyncEStateConfig();
        ApplySpeedBoost();
        ApplyTerrainCollisionIgnore(true);
        ApplyEnemyCollisionIgnore(true);
        SetGhostStateVisible(true);
        SetGhostShadowVisible(true);
        SetGhostParticlesVisible(true);
        Debug.Log(
            $"Player01 E 灵体疾行：持续={eDuration:F2}，移速倍率={eMoveSpeedMultiplier:F2}，回血={eHealPerTick:F2}/{eHealTickInterval:F2}秒，免疫怪物物理攻击={eImmuneToMonsterPhysicalDamage}，CD={eCooldown:F2}，蓝耗={eManaCost:F2}",
            this);

        if (Controller != null)
        {
            Controller.RestoreLocomotionAnimation(true);
        }
    }

    protected override IEnumerator CastRoutine()
    {
        float waitTime = Mathf.Max(0f, duration);
        float elapsed = 0f;
        float nextHealTickTime = Mathf.Max(0.01f, eHealTickInterval);
        while (elapsed < waitTime)
        {
            if (elapsed >= nextHealTickTime)
            {
                ApplyHealTick();
                nextHealTickTime += Mathf.Max(0.01f, eHealTickInterval);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        OnCastFinished();
        CompleteCast();
    }

    protected override void OnCastFinished()
    {
        IsRunningBoost = false;
        RestoreSpeed();
        ApplyTerrainCollisionIgnore(false);
        ApplyEnemyCollisionIgnore(false);
        SetGhostStateVisible(false);
        SetGhostShadowVisible(false);
        SetGhostParticlesVisible(false);
        Debug.Log("Player01 E 灵体疾行结束，已恢复移动速度/碰撞/受击状态", this);
    }

    protected override string GetSkillLabel()
    {
        return "E - Run Boost";
    }

    protected override int SkillIndex => 2;

    public bool IsImmuneToMonsterPhysicalDamage(BattleDamage damage)
    {
        return IsRunningBoost &&
               eImmuneToMonsterPhysicalDamage &&
               damage.damageType == BattleDamageType.Physical &&
               damage.source != null &&
               BattleTargetUtility.IsMonster(damage.source);
    }

    private void ApplySpeedBoost()
    {
        if (cachedMovement == null)
        {
            cachedMovement = GetComponent<PlayerMovement>();
        }

        if (cachedMovement == null)
        {
            return;
        }

        if (cachedOriginalMoveSpeed < 0f)
        {
            cachedOriginalMoveSpeed = cachedMovement.moveSpeed;
        }

        cachedMovement.moveSpeed = cachedOriginalMoveSpeed * Mathf.Max(0f, eMoveSpeedMultiplier);
    }

    private void RestoreSpeed()
    {
        if (cachedMovement == null)
        {
            cachedMovement = GetComponent<PlayerMovement>();
        }

        if (cachedMovement == null)
        {
            return;
        }

        if (cachedOriginalMoveSpeed >= 0f)
        {
            cachedMovement.moveSpeed = cachedOriginalMoveSpeed;
            cachedOriginalMoveSpeed = -1f;
        }
    }

    private void ApplyTerrainCollisionIgnore(bool enable)
    {
        if (!eIgnoreTerrainCollision)
        {
            return;
        }

        ApplyLayerCollisionIgnore(enable, terrainCollisionLayers, cachedTerrainCollisionStates, "[E - BrokenDash] Terrain layer mask is empty, skipping collision ignore.");
    }

    private void ApplyEnemyCollisionIgnore(bool enable)
    {
        if (!eIgnoreEnemyCollision)
        {
            return;
        }

        ApplyLayerCollisionIgnore(enable, enemyCollisionLayers, cachedEnemyCollisionStates, "[E - BrokenDash] Enemy layer mask is empty, skipping enemy collision ignore.");
    }

    private void ApplyLayerCollisionIgnore(bool enable, LayerMask layerMask, Dictionary<int, bool> cache, string emptyMaskWarning)
    {
        int playerLayer = gameObject.layer;
        int mask = layerMask.value;
        if (mask == 0)
        {
            if (debugLog && enable)
            {
                Debug.LogWarning(emptyMaskWarning, this);
            }

            return;
        }

        for (int layer = 0; layer < 32; layer++)
        {
            int layerBit = 1 << layer;
            if ((mask & layerBit) == 0)
            {
                continue;
            }

            if (enable)
            {
                if (!cache.ContainsKey(layer))
                {
                    cache[layer] = Physics.GetIgnoreLayerCollision(playerLayer, layer);
                }

                Physics.IgnoreLayerCollision(playerLayer, layer, true);
            }
            else if (cache.TryGetValue(layer, out bool originalState))
            {
                Physics.IgnoreLayerCollision(playerLayer, layer, originalState);
                cache.Remove(layer);
            }
        }
    }

    private void ApplyHealTick()
    {
        if (!IsRunningBoost || eHealPerTick <= 0f)
        {
            return;
        }

        if (cachedCombatHealth == null)
        {
            cachedCombatHealth = GetComponent<CombatHealth>();
        }

        cachedCombatHealth?.Heal(eHealPerTick);
    }


    protected override void OnDisable()
    {
        base.OnDisable();
        RestoreSpeed();
        ApplyTerrainCollisionIgnore(false);
        ApplyEnemyCollisionIgnore(false);
        IsRunningBoost = false;
        SetGhostStateVisible(false);
        SetGhostShadowVisible(false);
        SetGhostParticlesVisible(false);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        RestoreSpeed();
        ApplyTerrainCollisionIgnore(false);
        ApplyEnemyCollisionIgnore(false);
        IsRunningBoost = false;
        SetGhostStateVisible(false);
        SetGhostShadowVisible(false);
        SetGhostParticlesVisible(false);
    }

    private void SyncEStateConfig()
    {
        cooldown = Mathf.Max(0f, eCooldown);
        duration = Mathf.Max(0f, eDuration);

        float resolvedManaCost = Mathf.Max(0f, eManaCost);
        if (SkillResource != null && SkillIndex >= 0 && SkillResource.skillDatas != null && SkillResource.skillDatas.Length > SkillIndex)
        {
            SkillCostCDData eData = SkillResource.skillDatas[SkillIndex];
            eData.maxCooldown = cooldown;
            eData.manaCost = resolvedManaCost;
            SkillResource.skillDatas[SkillIndex] = eData;
        }

        if (Controller != null)
        {
            FieldInfo eCooldownField = typeof(Player01SkillController).GetField("eCooldown", BindingFlags.Instance | BindingFlags.NonPublic);
            if (eCooldownField != null)
            {
                eCooldownField.SetValue(Controller, cooldown);
            }
        }
    }

    private void CacheGhostStateVisual()
    {
        if (eGhostStateVisual != null)
        {
            return;
        }

        eGhostStateVisual = GetComponentInChildren<Player01GhostStateVisual>(true);
    }

    private void CacheGhostShadowFollower()
    {
        if (eGhostShadowFollower != null)
        {
            return;
        }

        eGhostShadowFollower = GetComponentInChildren<Player01EGhostShadowFollower>(true);
    }

    private void CacheGhostParticleController()
    {
        if (eGhostParticleController != null)
        {
            return;
        }

        eGhostParticleController = GetComponentInChildren<Player01EGhostParticleController>(true);
    }

    private void SetGhostStateVisible(bool visible)
    {
        if (!eEnableGhostStateVisual)
        {
            visible = false;
        }

        CacheGhostStateVisual();
        if (eGhostStateVisual == null)
        {
            return;
        }

        eGhostStateVisual.SetGhostActive(visible);
    }

    private void SetGhostShadowVisible(bool visible)
    {
        if (!eEnableGhostShadowFollower)
        {
            visible = false;
        }

        CacheGhostShadowFollower();
        if (eGhostShadowFollower == null)
        {
            if (debugLog)
            {
                Debug.Log("[E Shadow] ghostShadowFollower is null", this);
            }

            return;
        }

        eGhostShadowFollower.SetShadowActive(visible);
    }

    private void SetGhostParticlesVisible(bool visible)
    {
        if (!eEnableGhostParticles)
        {
            visible = false;
        }

        CacheGhostParticleController();
        if (eGhostParticleController == null)
        {
            if (debugLog)
            {
                Debug.Log("[E GhostParticles] controller is null", this);
            }

            return;
        }

        eGhostParticleController.SetGhostParticlesActive(visible);
    }
}
