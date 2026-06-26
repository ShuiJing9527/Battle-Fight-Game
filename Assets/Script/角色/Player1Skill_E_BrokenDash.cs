using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Player1Skill_E_BrokenDash : Player01SkillBase
{
    [Header("E - Run Boost")]
    [SerializeField, Min(0.1f)] private float speedMultiplier = 2f;
    [SerializeField] private bool ignoreObstacleCollision = true;
    [SerializeField] private LayerMask obstacleLayers = 1 << 3;
    [FormerlySerializedAs("dashDamage")]
    [SerializeField, Min(0f)] private float baseDamage = 16f;
    [SerializeField, Min(0f)] private float physicalScaling = 0.7f;
    [SerializeField, Min(0f)] private float specialScaling = 0.6f;
    [SerializeField, Min(0.1f)] private float dashHitRadius = 1.2f;
    [SerializeField] private LayerMask enemyLayer = ~0;

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
    private readonly Dictionary<int, bool> cachedLayerCollisionStates = new Dictionary<int, bool>();
    private readonly HashSet<CombatHealth> damagedCombatTargets = new HashSet<CombatHealth>();
    private readonly HashSet<EnemyHealth> damagedLegacyTargets = new HashSet<EnemyHealth>();

    private void Reset()
    {
        cooldown = 2.2f;
        duration = 3f;
        effectPower = 4f;
        animationName = "Run";
        debugLog = true;
        speedMultiplier = 2f;
        baseDamage = 16f;
        physicalScaling = 0.7f;
        specialScaling = 0.6f;
        dashHitRadius = 1.2f;
        enemyLayer = ~0;
        ignoreObstacleCollision = true;
        obstacleLayers = 1 << 3;
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
        cachedMovement = GetComponent<PlayerMovement>();
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

    protected override bool ShouldLoopAnimation()
    {
        return true;
    }

    protected override void OnCastStarted()
    {
        IsRunningBoost = true;
        damagedCombatTargets.Clear();
        damagedLegacyTargets.Clear();
        ApplySpeedBoost();
        ApplyObstacleCollisionIgnore(true);
        SetGhostStateVisible(true);
        SetGhostShadowVisible(true);
        SetGhostParticlesVisible(true);

        if (debugLog)
        {
            Debug.Log($"[Player01 E Run] start duration={duration:F2}, animation={animationName}, speedMultiplier={speedMultiplier:F2}, ignoreObstacleCollision={ignoreObstacleCollision}", this);
        }

        if (Controller != null)
        {
            Controller.RestoreLocomotionAnimation(true);
        }
    }

    protected override IEnumerator CastRoutine()
    {
        float waitTime = Mathf.Max(0f, duration);
        float elapsed = 0f;
        while (elapsed < waitTime)
        {
            ApplyDashDamage();
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
        ApplyObstacleCollisionIgnore(false);
        SetGhostStateVisible(false);
        SetGhostShadowVisible(false);
        SetGhostParticlesVisible(false);

        if (debugLog)
        {
            Debug.Log("[Player01 E Run] end restore movement/collision", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "E - Run Boost";
    }

    protected override int SkillIndex => 2;

    private void ApplyDashDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, dashHitRadius, enemyLayer, QueryTriggerInteraction.Collide);
        float finalDamage = ResolveDamage();

        foreach (Collider hit in hits)
        {
            if (!BattleTargetUtility.IsMonster(hit, transform))
            {
                continue;
            }

            CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(hit, transform);
            if (combatHealth != null && damagedCombatTargets.Add(combatHealth))
            {
                combatHealth.TakeDamage(new BattleDamage(finalDamage, BattleDamageType.Physical, gameObject));
                continue;
            }

            EnemyHealth legacyHealth = BattleTargetUtility.GetMonsterLegacyHealth(hit, transform);
            if (legacyHealth != null && damagedLegacyTargets.Add(legacyHealth))
            {
                legacyHealth.TakeDamage(Mathf.RoundToInt(finalDamage), gameObject);
            }
        }
    }

    private float ResolveDamage()
    {
        return PlayerSkillDamageUtility.CalculateHybridSkillDamage(
            this,
            gameObject,
            baseDamage,
            physicalScaling,
            specialScaling,
            "Player01 E");
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

        cachedMovement.moveSpeed = cachedOriginalMoveSpeed * Mathf.Max(0.1f, speedMultiplier);
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

    private void ApplyObstacleCollisionIgnore(bool enable)
    {
        if (!ignoreObstacleCollision)
        {
            return;
        }

        int playerLayer = gameObject.layer;
        int mask = obstacleLayers.value;
        if (mask == 0)
        {
            if (debugLog && enable)
            {
                Debug.LogWarning("[E - BrokenDash] Obstacle layer mask is empty, skipping collision ignore.", this);
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
                if (!cachedLayerCollisionStates.ContainsKey(layer))
                {
                    cachedLayerCollisionStates[layer] = Physics.GetIgnoreLayerCollision(playerLayer, layer);
                }

                Physics.IgnoreLayerCollision(playerLayer, layer, true);
            }
            else if (cachedLayerCollisionStates.TryGetValue(layer, out bool originalState))
            {
                Physics.IgnoreLayerCollision(playerLayer, layer, originalState);
                cachedLayerCollisionStates.Remove(layer);
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        SetGhostStateVisible(false);
        SetGhostShadowVisible(false);
        SetGhostParticlesVisible(false);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SetGhostStateVisible(false);
        SetGhostShadowVisible(false);
        SetGhostParticlesVisible(false);
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
