using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

public class Player1Skill_E_BrokenDash : Player01SkillBase
{
    private const float MoveSpeedEpsilon = 0.001f;
    private const float NightBuffDurationMultiplier = 1.2f;
    private const float NightBuffHealMultiplier = 1.5f;
    private const float NightBuffNextSkillDamageMultiplier = 1.2f;
    private const float NightBuffNextSkillDamageDuration = 2f;
    private static readonly string[] GroundLikeKeywords = { "ground", "floor", "terrain", "platform" };
    private static readonly string[] TerrainObstacleKeywords = { "wall", "airwall", "obstacle", "barrier", "block" };
    private static readonly string[] EnemyLikeKeywords = { "enemy", "monster", "elite", "boss", "slime" };

    [Header("E - 灵体疾行 / 核心参数")]
    [SerializeField, Min(0f)] private float eCooldown = 10f;
    [SerializeField, Min(0f)] private float eDuration = 3f;
    [SerializeField, Min(0f)] private float eManaCost = 30f;

    [Header("E - 灵体疾行 / 移动")]
    [SerializeField, Min(0f)] private float eMoveSpeedMultiplier = 2.25f;

    [Header("E - 灵体疾行 / 回复")]
    [SerializeField, Min(0f)] private float eHealPerTick = 0f;
    [SerializeField, Range(0f, 1f)] private float eHealPercentPerSecond = 0.10f;
    [SerializeField, Min(0.01f)] private float eHealTickInterval = 0.5f;

    [Header("E - 灵体疾行 / 灵体状态")]
    [FormerlySerializedAs("ignoreObstacleCollision")]
    [SerializeField] private bool eIgnoreTerrainCollision = true;
    [SerializeField] private bool eIgnoreEnemyCollision = true;
    [SerializeField] private bool eImmuneToMonsterPhysicalDamage = true;
    [FormerlySerializedAs("obstacleLayers")]
    [SerializeField] private LayerMask terrainCollisionLayers = 0;
    [SerializeField] private LayerMask enemyCollisionLayers = 0;

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
    private float cachedBoostedMoveSpeed = -1f;
    private float lastKnownStableMoveSpeed = -1f;
    private readonly List<IgnoredColliderPair> ignoredTerrainCollisionPairs = new List<IgnoredColliderPair>();
    private readonly HashSet<long> ignoredTerrainCollisionPairKeys = new HashSet<long>();
    private readonly List<IgnoredColliderPair> ignoredEnemyCollisionPairs = new List<IgnoredColliderPair>();
    private readonly HashSet<long> ignoredEnemyCollisionPairKeys = new HashSet<long>();
    private CombatHealth cachedCombatHealth;
    private Rigidbody cachedRigidbody;
    private RigidbodyConstraints cachedRigidbodyConstraints;
    private float cachedGhostStartY;
    private bool hasGroundSafetyLock;
    private bool nightBuffEmpoweredThisCast;

    private void LateUpdate()
    {
        if (IsRunningBoost)
        {
            MaintainSpeedBoost();
            return;
        }

        TrackStableMoveSpeed();
    }

    private void Reset()
    {
        cooldown = 2.2f;
        duration = 3f;
        effectPower = 4f;
        animationName = "Run";
        debugLog = true;
        eCooldown = 10f;
        eDuration = 3f;
        eManaCost = 30f;
        eMoveSpeedMultiplier = 2.25f;
        eHealPercentPerSecond = 0.10f;
        eHealTickInterval = 0.5f;
        eIgnoreTerrainCollision = true;
        eIgnoreEnemyCollision = true;
        eImmuneToMonsterPhysicalDamage = true;
        ePreventFallThroughGround = true;
        eLockYPositionDuringGhost = true;
        terrainCollisionLayers = BuildLayerMask("Wall", "AirWall", "Obstacle");
        enemyCollisionLayers = BuildLayerMask("Enemy", "Monster");
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
        SanitizeCollisionMasks();
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
        SanitizeCollisionMasks();
    }

    private void OnValidate()
    {
        SyncEStateConfig();
        SanitizeCollisionMasks();
    }

    protected override bool ShouldLoopAnimation()
    {
        return true;
    }

    protected override void OnCastStarted()
    {
        ResolvePlayerRuneRuntimeState();
        IsRunningBoost = true;
        nightBuffEmpoweredThisCast = DayNightAffinityDamageModifier.IsNightChildBuffActive(Controller != null ? Controller.gameObject : gameObject);
        SyncEStateConfig();
        duration = Mathf.Max(0f, nightBuffEmpoweredThisCast ? eDuration * NightBuffDurationMultiplier : eDuration);
        BeginGroundSafetyLock();
        ApplySpeedBoost();
        ApplyTerrainCollisionIgnore(true);
        ApplyEnemyCollisionIgnore(true);
        SetGhostStateVisible(true);
        SetGhostShadowVisible(true);
        SetGhostParticlesVisible(true);
        if (nightBuffEmpoweredThisCast)
        {
            Debug.Log($"[SecondBuffDebug] Player01 E night buff active. duration x{NightBuffDurationMultiplier:F2}, heal x{NightBuffHealMultiplier:F2}.", this);
        }
        Debug.Log(
            $"Player01 E 灵体疾行：持续={eDuration:F2}，移速倍率={eMoveSpeedMultiplier:F2}，每秒回血={eHealPercentPerSecond:P0} MaxHP，免疫怪物物理攻击={eImmuneToMonsterPhysicalDamage}，CD={eCooldown:F2}，蓝耗={eManaCost:F2}",
            this);

        if (Controller != null)
        {
            Controller.TryPlaySkillAnimation(animationName, true);
        }
    }

    protected override IEnumerator CastRoutine()
    {
        float waitTime = Mathf.Max(0f, duration);
        float elapsed = 0f;
        while (elapsed < waitTime)
        {
            ApplyContinuousHeal(Time.deltaTime);
            MaintainSpeedBoost();
            elapsed += Time.deltaTime;
            RefreshTerrainCollisionIgnores();
            RefreshEnemyCollisionIgnores();
            EnforceGroundSafetyLock();
            yield return null;
        }

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
        if (nightBuffEmpoweredThisCast)
        {
            PlayerNextSkillDamageBoostStatus.ApplyOrRefresh(
                Controller != null ? Controller.gameObject : gameObject,
                NightBuffNextSkillDamageMultiplier,
                NightBuffNextSkillDamageDuration);
            Debug.Log($"[SecondBuffDebug] Player01 E granted next skill damage boost x{NightBuffNextSkillDamageMultiplier:F2} for {NightBuffNextSkillDamageDuration:F2}s.", this);
        }

        nightBuffEmpoweredThisCast = false;
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

        TrackStableMoveSpeed();
        float currentMoveSpeed = Mathf.Max(0f, cachedMovement.moveSpeed);
        float resolvedBaseMoveSpeed = Mathf.Max(currentMoveSpeed, lastKnownStableMoveSpeed);
        if (resolvedBaseMoveSpeed <= 0f)
        {
            resolvedBaseMoveSpeed = Mathf.Max(0f, currentMoveSpeed);
        }

        cachedOriginalMoveSpeed = resolvedBaseMoveSpeed;
        float effectiveMoveSpeedMultiplier = ResolveEffectiveEMoveSpeedMultiplier();
        cachedBoostedMoveSpeed = resolvedBaseMoveSpeed * effectiveMoveSpeedMultiplier;
        cachedMovement.moveSpeed = cachedBoostedMoveSpeed;
        WakePlayerRigidbody();
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
            float currentMoveSpeed = Mathf.Max(0f, cachedMovement.moveSpeed);
            if (cachedBoostedMoveSpeed <= 0f || currentMoveSpeed <= cachedBoostedMoveSpeed + MoveSpeedEpsilon)
            {
                cachedMovement.moveSpeed = cachedOriginalMoveSpeed;
            }
        }

        cachedOriginalMoveSpeed = -1f;
        cachedBoostedMoveSpeed = -1f;
    }

    private void ApplyTerrainCollisionIgnore(bool enable)
    {
        if (!eIgnoreTerrainCollision)
        {
            return;
        }

        if (enable)
        {
            RefreshTerrainCollisionIgnores();
        }
        else
        {
            RestoreTerrainCollisionIgnores();
        }
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

    private void ApplyContinuousHeal(float deltaTime)
    {
        if (!IsRunningBoost || deltaTime <= 0f)
        {
            return;
        }

        if (cachedCombatHealth == null)
        {
            cachedCombatHealth = GetComponent<CombatHealth>();
        }

        if (cachedCombatHealth == null)
        {
            return;
        }

        float maxHealth = cachedCombatHealth.MaxHealthValue;
        if (maxHealth <= 0f || eHealPercentPerSecond <= 0f)
        {
            return;
        }

        float healAmount = maxHealth * eHealPercentPerSecond * deltaTime;
        if (nightBuffEmpoweredThisCast)
        {
            healAmount *= NightBuffHealMultiplier;
        }

        if (healAmount > 0f)
        {
            cachedCombatHealth.Heal(healAmount);
        }
    }

    private void RefreshTerrainCollisionIgnores()
    {
        if (!IsRunningBoost || !eIgnoreTerrainCollision)
        {
            return;
        }

        Collider[] playerColliders = GetComponentsInChildren<Collider>(true);
        if (playerColliders == null || playerColliders.Length == 0)
        {
            return;
        }

        Collider[] worldColliders = Object.FindObjectsOfType<Collider>(true);
        if (worldColliders == null || worldColliders.Length == 0)
        {
            return;
        }

        foreach (Collider playerCollider in playerColliders)
        {
            if (!IsUsablePhysicalCollider(playerCollider))
            {
                continue;
            }

            for (int i = 0; i < worldColliders.Length; i++)
            {
                Collider targetCollider = worldColliders[i];
                if (!ShouldIgnoreTerrainCollider(targetCollider))
                {
                    continue;
                }

                AddIgnoredCollisionPair(playerCollider, targetCollider, ignoredTerrainCollisionPairs, ignoredTerrainCollisionPairKeys);
            }
        }
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

        Collider[] worldColliders = Object.FindObjectsOfType<Collider>(true);
        if (worldColliders == null || worldColliders.Length == 0)
        {
            return;
        }

        foreach (Collider playerCollider in playerColliders)
        {
            if (!IsUsablePhysicalCollider(playerCollider))
            {
                continue;
            }

            for (int i = 0; i < worldColliders.Length; i++)
            {
                Collider monsterCollider = worldColliders[i];
                if (!IsUsablePhysicalCollider(monsterCollider))
                {
                    continue;
                }

                if (!ShouldIgnoreEnemyCollider(monsterCollider))
                {
                    continue;
                }

                AddIgnoredCollisionPair(playerCollider, monsterCollider, ignoredEnemyCollisionPairs, ignoredEnemyCollisionPairKeys);
            }
        }
    }

    private bool ShouldIgnoreTerrainCollider(Collider targetCollider)
    {
        return IsUsablePhysicalCollider(targetCollider) &&
               !targetCollider.transform.IsChildOf(transform) &&
               !IsGroundLikeCollider(targetCollider) &&
               (IsLayerIncluded(terrainCollisionLayers, targetCollider.gameObject.layer) || IsTerrainObstacleLikeCollider(targetCollider));
    }

    private bool ShouldIgnoreEnemyCollider(Collider targetCollider)
    {
        return IsUsablePhysicalCollider(targetCollider) &&
               !targetCollider.transform.IsChildOf(transform) &&
               !IsGroundLikeCollider(targetCollider) &&
               (IsLayerIncluded(enemyCollisionLayers, targetCollider.gameObject.layer) || IsMonsterLikeCollider(targetCollider));
    }

    private void AddIgnoredCollisionPair(
        Collider playerCollider,
        Collider targetCollider,
        List<IgnoredColliderPair> pairCache,
        HashSet<long> pairKeys)
    {
        long key = GetColliderPairKey(playerCollider, targetCollider);
        if (!pairKeys.Add(key))
        {
            return;
        }

        bool originalState = Physics.GetIgnoreCollision(playerCollider, targetCollider);
        if (!originalState)
        {
            Physics.IgnoreCollision(playerCollider, targetCollider, true);
            WakePlayerRigidbody();
        }

        pairCache.Add(new IgnoredColliderPair(playerCollider, targetCollider, originalState));
    }

    private void RestoreTerrainCollisionIgnores()
    {
        RestoreIgnoredCollisionPairs(ignoredTerrainCollisionPairs, ignoredTerrainCollisionPairKeys);
    }

    private void RestoreEnemyCollisionIgnores()
    {
        RestoreIgnoredCollisionPairs(ignoredEnemyCollisionPairs, ignoredEnemyCollisionPairKeys);
    }

    private static void RestoreIgnoredCollisionPairs(List<IgnoredColliderPair> pairCache, HashSet<long> pairKeys)
    {
        for (int i = pairCache.Count - 1; i >= 0; i--)
        {
            IgnoredColliderPair pair = pairCache[i];
            if (pair.playerCollider != null && pair.targetCollider != null)
            {
                Physics.IgnoreCollision(pair.playerCollider, pair.targetCollider, pair.originalIgnored);
            }
        }

        pairCache.Clear();
        pairKeys.Clear();
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

    private static bool IsGroundLikeLayer(int layer)
    {
        if (layer < 0)
        {
            return false;
        }

        string layerName = LayerMask.LayerToName(layer);
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return false;
        }

        return layerName == "Ground" ||
               layerName == "Floor" ||
               layerName == "Terrain" ||
               layerName == "Platform";
    }

    private bool IsGroundLikeCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (IsGroundLikeLayer(collider.gameObject.layer))
        {
            return true;
        }

        return MatchesAnyKeyword(collider.name, GroundLikeKeywords) ||
               MatchesAnyKeyword(collider.gameObject.name, GroundLikeKeywords) ||
               MatchesAnyKeyword(collider.tag, GroundLikeKeywords);
    }

    private bool IsTerrainObstacleLikeCollider(Collider collider)
    {
        if (collider == null || IsGroundLikeCollider(collider))
        {
            return false;
        }

        return MatchesAnyKeyword(collider.name, TerrainObstacleKeywords) ||
               MatchesAnyKeyword(collider.gameObject.name, TerrainObstacleKeywords) ||
               MatchesAnyKeyword(collider.tag, TerrainObstacleKeywords);
    }

    private bool IsMonsterLikeCollider(Collider collider)
    {
        if (collider == null || IsGroundLikeCollider(collider))
        {
            return false;
        }

        if (BattleTargetUtility.IsMonster(collider.gameObject))
        {
            return true;
        }

        if (collider.attachedRigidbody != null && BattleTargetUtility.IsMonster(collider.attachedRigidbody.gameObject))
        {
            return true;
        }

        if (collider.GetComponentInParent<MonsterIdentity>() != null || collider.GetComponentInParent<EnemyController>() != null)
        {
            return true;
        }

        return MatchesAnyKeyword(collider.name, EnemyLikeKeywords) ||
               MatchesAnyKeyword(collider.gameObject.name, EnemyLikeKeywords) ||
               MatchesAnyKeyword(collider.tag, EnemyLikeKeywords);
    }

    private static bool MatchesAnyKeyword(string source, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(source) || keywords == null)
        {
            return false;
        }

        string lowered = source.ToLowerInvariant();
        for (int i = 0; i < keywords.Length; i++)
        {
            if (lowered.Contains(keywords[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static LayerMask BuildLayerMask(params string[] layerNames)
    {
        int mask = 0;
        for (int i = 0; i < layerNames.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layerNames[i]);
            if (layer >= 0)
            {
                mask |= 1 << layer;
            }
        }

        return mask;
    }

    private void SanitizeCollisionMasks()
    {
        terrainCollisionLayers = SanitizeMask(
            terrainCollisionLayers,
            fallbackIfEverything: BuildLayerMask("Wall", "AirWall", "Obstacle"));
        enemyCollisionLayers = SanitizeMask(
            enemyCollisionLayers,
            fallbackIfEverything: BuildLayerMask("Enemy", "Monster"));
    }

    private static LayerMask SanitizeMask(LayerMask sourceMask, LayerMask fallbackIfEverything)
    {
        int maskValue = sourceMask.value;
        if (maskValue == ~0 && fallbackIfEverything.value != 0)
        {
            maskValue = fallbackIfEverything.value;
        }

        for (int layer = 0; layer < 32; layer++)
        {
            if ((maskValue & (1 << layer)) == 0)
            {
                continue;
            }

            if (IsGroundLikeLayer(layer))
            {
                maskValue &= ~(1 << layer);
            }
        }

        return maskValue;
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
        public readonly Collider targetCollider;
        public readonly bool originalIgnored;

        public IgnoredColliderPair(Collider playerCollider, Collider targetCollider, bool originalIgnored)
        {
            this.playerCollider = playerCollider;
            this.targetCollider = targetCollider;
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
        Vector3 velocityBeforeWrite = cachedRigidbody.linearVelocity;
        Vector3 velocityAfterWrite = new Vector3(cachedRigidbody.linearVelocity.x, 0f, cachedRigidbody.linearVelocity.z);
        cachedRigidbody.linearVelocity = velocityAfterWrite;
        PlayerMovement.LogVelocityWrite(
            this,
            nameof(Player1Skill_E_BrokenDash),
            nameof(BeginGroundSafetyLock),
            cachedRigidbody,
            velocityBeforeWrite,
            velocityAfterWrite,
            "begin-ground-safety-lock",
            "broken-dash-ground-lock",
            "none",
            "runtime");
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
            Vector3 velocityBeforeWrite = cachedRigidbody.linearVelocity;
            Vector3 velocityAfterWrite = new Vector3(cachedRigidbody.linearVelocity.x, 0f, cachedRigidbody.linearVelocity.z);
            cachedRigidbody.linearVelocity = velocityAfterWrite;
            PlayerMovement.LogVelocityWrite(
                this,
                nameof(Player1Skill_E_BrokenDash),
                nameof(EnforceGroundSafetyLock),
                cachedRigidbody,
                velocityBeforeWrite,
                velocityAfterWrite,
                "enforce-ground-safety-lock",
                "broken-dash-ground-lock",
                "none",
                "runtime");
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
            Vector3 velocityBeforeWrite = cachedRigidbody.linearVelocity;
            Vector3 velocityAfterWrite = new Vector3(cachedRigidbody.linearVelocity.x, 0f, cachedRigidbody.linearVelocity.z);
            cachedRigidbody.linearVelocity = velocityAfterWrite;
            PlayerMovement.LogVelocityWrite(
                this,
                nameof(Player1Skill_E_BrokenDash),
                nameof(EndGroundSafetyLock),
                cachedRigidbody,
                velocityBeforeWrite,
                velocityAfterWrite,
                "end-ground-safety-lock",
                "broken-dash-ground-lock",
                "none",
                "runtime");
        }

        hasGroundSafetyLock = false;
    }

    private void MaintainSpeedBoost()
    {
        if (!IsRunningBoost || cachedMovement == null)
        {
            return;
        }

        if (cachedBoostedMoveSpeed <= 0f)
        {
            ApplySpeedBoost();
            return;
        }

        float currentMoveSpeed = Mathf.Max(0f, cachedMovement.moveSpeed);
        if (currentMoveSpeed + MoveSpeedEpsilon < cachedBoostedMoveSpeed)
        {
            cachedMovement.moveSpeed = cachedBoostedMoveSpeed;
        }
    }

    private void TrackStableMoveSpeed()
    {
        if (cachedMovement == null)
        {
            cachedMovement = GetComponent<PlayerMovement>();
        }

        if (cachedMovement == null)
        {
            return;
        }

        float currentMoveSpeed = Mathf.Max(0f, cachedMovement.moveSpeed);
        if (currentMoveSpeed <= 0f)
        {
            return;
        }

        if (cachedBoostedMoveSpeed > 0f && Mathf.Abs(currentMoveSpeed - cachedBoostedMoveSpeed) <= MoveSpeedEpsilon)
        {
            return;
        }

        lastKnownStableMoveSpeed = currentMoveSpeed;
    }

    private float ResolveEffectiveEMoveSpeedMultiplier()
    {
        float beforeMultiplier = Mathf.Max(0f, eMoveSpeedMultiplier);
        float boostAboveBase = Mathf.Max(0f, beforeMultiplier - 1f);
        float effectiveMultiplier = 1f + boostAboveBase * ResolveManaRuneScaledMultiplier(0.25f);
        if (effectiveMultiplier > beforeMultiplier)
        {
            LogManaRuneApplied("E", "MoveSpeedMultiplier", beforeMultiplier, effectiveMultiplier);
        }

        return effectiveMultiplier;
    }

    private void WakePlayerRigidbody()
    {
        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        if (cachedRigidbody != null)
        {
            cachedRigidbody.WakeUp();
        }
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
