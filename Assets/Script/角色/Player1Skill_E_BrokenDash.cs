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

    [Header("E - Fall Protection")]
    [SerializeField] private bool ePreventFallThroughGround = true;
    [SerializeField] private bool eLockYPositionDuringGhost = true;

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
    private readonly List<IgnoredColliderPair> ignoredEnemyCollisionPairs = new List<IgnoredColliderPair>();
    private readonly HashSet<long> ignoredEnemyCollisionPairKeys = new HashSet<long>();
    private CombatHealth cachedCombatHealth;
    private Rigidbody cachedRigidbody;
    private RigidbodyConstraints cachedRigidbodyConstraints;
    private float cachedGhostStartY;
    private bool hasGroundSafetyLock;

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
        ePreventFallThroughGround = true;
        eLockYPositionDuringGhost = true;
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
        BeginGroundSafetyLock();
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
            RefreshEnemyCollisionIgnores();
            EnforceGroundSafetyLock();
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
        EndGroundSafetyLock();
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

        if (ePreventFallThroughGround)
        {
            if (debugLog && enable)
            {
                Debug.Log("[E - BrokenDash] Terrain collision ignore skipped by fall protection. Enemy collision ignore still applies.", this);
            }

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

        if (enable)
        {
            RefreshEnemyCollisionIgnores();
        }
        else
        {
            RestoreEnemyCollisionIgnores();
        }
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

    private void RefreshEnemyCollisionIgnores()
    {
        if (!IsRunningBoost || !eIgnoreEnemyCollision)
        {
            return;
        }

        Collider[] playerColliders = GetComponentsInChildren<Collider>(true);
        if (playerColliders == null || playerColliders.Length == 0)
        {
            return;
        }

        HashSet<Collider> monsterColliders = new HashSet<Collider>();
        MonsterIdentity[] identities = Object.FindObjectsOfType<MonsterIdentity>(true);
        for (int i = 0; i < identities.Length; i++)
        {
            CollectMonsterColliders(identities[i], monsterColliders);
        }

        EnemyController[] enemies = Object.FindObjectsOfType<EnemyController>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            CollectMonsterColliders(enemies[i], monsterColliders);
        }

        foreach (Collider playerCollider in playerColliders)
        {
            if (!IsUsablePhysicalCollider(playerCollider))
            {
                continue;
            }

            foreach (Collider monsterCollider in monsterColliders)
            {
                if (!IsUsablePhysicalCollider(monsterCollider))
                {
                    continue;
                }

                if (monsterCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!IsLayerIncluded(enemyCollisionLayers, monsterCollider.gameObject.layer))
                {
                    continue;
                }

                AddIgnoredEnemyCollisionPair(playerCollider, monsterCollider);
            }
        }
    }

    private void CollectMonsterColliders(Component monsterComponent, HashSet<Collider> output)
    {
        if (monsterComponent == null || output == null)
        {
            return;
        }

        if (!BattleTargetUtility.IsMonster(monsterComponent.gameObject))
        {
            return;
        }

        Collider[] colliders = monsterComponent.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (IsUsablePhysicalCollider(collider))
            {
                output.Add(collider);
            }
        }
    }

    private void AddIgnoredEnemyCollisionPair(Collider playerCollider, Collider monsterCollider)
    {
        long key = GetColliderPairKey(playerCollider, monsterCollider);
        if (!ignoredEnemyCollisionPairKeys.Add(key))
        {
            return;
        }

        bool originalState = Physics.GetIgnoreCollision(playerCollider, monsterCollider);
        if (!originalState)
        {
            Physics.IgnoreCollision(playerCollider, monsterCollider, true);
        }

        ignoredEnemyCollisionPairs.Add(new IgnoredColliderPair(playerCollider, monsterCollider, originalState));
    }

    private void RestoreEnemyCollisionIgnores()
    {
        for (int i = ignoredEnemyCollisionPairs.Count - 1; i >= 0; i--)
        {
            IgnoredColliderPair pair = ignoredEnemyCollisionPairs[i];
            if (pair.playerCollider != null && pair.monsterCollider != null)
            {
                Physics.IgnoreCollision(pair.playerCollider, pair.monsterCollider, pair.originalIgnored);
            }
        }

        ignoredEnemyCollisionPairs.Clear();
        ignoredEnemyCollisionPairKeys.Clear();
    }

    private static bool IsUsablePhysicalCollider(Collider collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.activeInHierarchy &&
               !collider.isTrigger;
    }

    private static bool IsLayerIncluded(LayerMask mask, int layer)
    {
        int maskValue = mask.value;
        return maskValue != 0 && (maskValue & (1 << layer)) != 0;
    }

    private static long GetColliderPairKey(Collider a, Collider b)
    {
        int aId = a.GetInstanceID();
        int bId = b.GetInstanceID();
        if (aId > bId)
        {
            int temp = aId;
            aId = bId;
            bId = temp;
        }

        return ((long)(uint)aId << 32) | (uint)bId;
    }

    private readonly struct IgnoredColliderPair
    {
        public readonly Collider playerCollider;
        public readonly Collider monsterCollider;
        public readonly bool originalIgnored;

        public IgnoredColliderPair(Collider playerCollider, Collider monsterCollider, bool originalIgnored)
        {
            this.playerCollider = playerCollider;
            this.monsterCollider = monsterCollider;
            this.originalIgnored = originalIgnored;
        }
    }

    private void BeginGroundSafetyLock()
    {
        if (!ePreventFallThroughGround || !eLockYPositionDuringGhost)
        {
            hasGroundSafetyLock = false;
            return;
        }

        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        cachedGhostStartY = transform.position.y;
        hasGroundSafetyLock = true;

        if (cachedRigidbody == null)
        {
            return;
        }

        cachedRigidbodyConstraints = cachedRigidbody.constraints;
        cachedRigidbody.constraints = cachedRigidbodyConstraints | RigidbodyConstraints.FreezePositionY;
        cachedRigidbody.linearVelocity = new Vector3(cachedRigidbody.linearVelocity.x, 0f, cachedRigidbody.linearVelocity.z);
        cachedRigidbody.angularVelocity = Vector3.zero;
    }

    private void EnforceGroundSafetyLock()
    {
        if (!hasGroundSafetyLock || !ePreventFallThroughGround || !eLockYPositionDuringGhost)
        {
            return;
        }

        Vector3 position = transform.position;
        if (!Mathf.Approximately(position.y, cachedGhostStartY))
        {
            position.y = cachedGhostStartY;
            transform.position = position;
        }

        if (cachedRigidbody != null)
        {
            cachedRigidbody.linearVelocity = new Vector3(cachedRigidbody.linearVelocity.x, 0f, cachedRigidbody.linearVelocity.z);
        }
    }

    private void EndGroundSafetyLock()
    {
        if (!hasGroundSafetyLock)
        {
            return;
        }

        EnforceGroundSafetyLock();

        if (cachedRigidbody != null)
        {
            cachedRigidbody.constraints = cachedRigidbodyConstraints;
            cachedRigidbody.linearVelocity = new Vector3(cachedRigidbody.linearVelocity.x, 0f, cachedRigidbody.linearVelocity.z);
        }

        hasGroundSafetyLock = false;
    }


    protected override void OnDisable()
    {
        base.OnDisable();
        RestoreSpeed();
        ApplyTerrainCollisionIgnore(false);
        ApplyEnemyCollisionIgnore(false);
        EndGroundSafetyLock();
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
        EndGroundSafetyLock();
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
