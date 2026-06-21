using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player1Skill_W_ThreadFlow : Player01SkillBase
{
    [Header("W - 帷幕护罩 / 基础")]
    [SerializeField, Min(0f)] private float wDuration = 8f;
    [SerializeField, Min(0f)] private float wCooldown = 8f;
    [SerializeField, Min(0.1f)] private float wRadius = 4.5f;
    [SerializeField] private bool wFollowPlayer = true;

    [Header("W - 帷幕护罩 / 减伤")]
    [SerializeField, Range(0f, 1f)] private float playerDamageTakenMultiplier = 0.5f;

    [Header("W - 帷幕护罩 / 敌人减益")]
    [SerializeField, Range(0f, 1f)] private float enemyMoveSpeedMultiplier = 0.5f;
    [SerializeField, Range(0f, 1f)] private float enemyAttackMultiplier = 0.5f;
    [SerializeField, Min(0.01f)] private float enemyDebuffRefreshInterval = 0.1f;

    [Header("W - 帷幕护罩 / 视觉")]
    [SerializeField] private GameObject veilBarrierPrefab;
    [SerializeField] private Vector3 veilBarrierScale = new Vector3(45f, 45f, 45f);
    [SerializeField] private Vector3 veilBarrierLocalOffset = Vector3.zero;

    [Header("Legacy")]
    [SerializeField, HideInInspector, Min(0f)] private float damageReduction = 0.4f;
    [SerializeField, HideInInspector] private GameObject shieldPrefab;

    public bool IsDefending { get; private set; }

    private readonly HashSet<EnemyDebuffReceiver> activeDebuffedEnemies = new HashSet<EnemyDebuffReceiver>();
    private readonly HashSet<EnemyDebuffReceiver> currentDebuffedEnemies = new HashSet<EnemyDebuffReceiver>();
    private CombatHealth cachedCombatHealth;
    private GameObject activeBarrierInstance;
    private Coroutine enemyDebuffRoutine;
    private string damageModifierKey;
    private const string VeilDebuffKey = "Player01_W_Veil";

    private void Reset()
    {
        wDuration = 8f;
        wCooldown = 8f;
        wRadius = 4.5f;
        wFollowPlayer = true;
        effectPower = 0.8f;
        animationName = "";
        debugLog = true;
        playerDamageTakenMultiplier = 0.5f;
        enemyMoveSpeedMultiplier = 0.5f;
        enemyAttackMultiplier = 0.5f;
        enemyDebuffRefreshInterval = 0.1f;
        veilBarrierScale = new Vector3(45f, 45f, 45f);
        veilBarrierLocalOffset = Vector3.zero;
        SyncSkillTiming();
    }

    private void Awake()
    {
        SyncSkillTiming();
        CacheReferences();
    }

    private void OnValidate()
    {
        SyncSkillTiming();
        CacheReferences();
    }

    private void LateUpdate()
    {
        ApplyBarrierTransform();
    }

    public override bool LocksLocomotionAnimation()
    {
        return false;
    }

    protected override void OnCastStarted()
    {
        IsDefending = true;
        SyncSkillTiming();
        CacheReferences();
        activeDebuffedEnemies.Clear();
        currentDebuffedEnemies.Clear();
        ApplyPlayerDamageModifier();
        CreateBarrierInstance();
        StartEnemyDebuffRoutine();
        Controller?.FinishSkill(this);

        if (debugLog)
        {
            Debug.Log("[Player01 W Veil] barrier active, skills remain usable", this);
            Debug.Log($"[Player01 W Veil] Start duration={wDuration:F2} radius={wRadius:F2}", this);
        }
    }

    protected override void OnCastFinished()
    {
        IsDefending = false;
        StopEnemyDebuffRoutine();
        RemovePlayerDamageModifier();
        ClearEnemyDebuffs();
        DestroyBarrierInstance();

        if (debugLog)
        {
            Debug.Log("[Player01 W Veil] End", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "W - 帷幕护罩 / Veil Barrier";
    }

    private void SyncSkillTiming()
    {
        duration = Mathf.Max(0f, wDuration);
        cooldown = Mathf.Max(0f, wCooldown);
    }

    private void CacheReferences()
    {
        if (veilBarrierPrefab == null && shieldPrefab != null)
        {
            veilBarrierPrefab = shieldPrefab;
        }
        else if (shieldPrefab == null && veilBarrierPrefab != null)
        {
            shieldPrefab = veilBarrierPrefab;
        }

        if (cachedCombatHealth == null)
        {
            cachedCombatHealth = GetComponent<CombatHealth>();
        }

        if (cachedCombatHealth == null)
        {
            cachedCombatHealth = GetComponentInParent<CombatHealth>();
        }

        if (string.IsNullOrWhiteSpace(damageModifierKey))
        {
            damageModifierKey = VeilDebuffKey;
        }
    }

    private void ApplyPlayerDamageModifier()
    {
        if (cachedCombatHealth == null)
        {
            CacheReferences();
        }

        if (cachedCombatHealth == null)
        {
            return;
        }

        float multiplier = Mathf.Clamp(playerDamageTakenMultiplier, 0f, 1f);
        cachedCombatHealth.AddDamageReductionModifier(damageModifierKey, multiplier);

        if (debugLog)
        {
            Debug.Log($"[Player01 W Veil] Player damage multiplier = {multiplier:F2}", this);
        }
    }

    private void RemovePlayerDamageModifier()
    {
        if (cachedCombatHealth == null)
        {
            CacheReferences();
        }

        if (cachedCombatHealth == null)
        {
            return;
        }

        cachedCombatHealth.RemoveDamageReductionModifier(damageModifierKey);
    }

    private void CreateBarrierInstance()
    {
        DestroyBarrierInstance();

        GameObject barrierPrefab = ResolveBarrierPrefab();
        if (barrierPrefab == null)
        {
            if (debugLog)
            {
                Debug.LogWarning("[Player01 W Veil] Barrier prefab is missing.", this);
            }

            return;
        }

        if (wFollowPlayer)
        {
            activeBarrierInstance = Instantiate(barrierPrefab, transform);
            activeBarrierInstance.transform.localPosition = veilBarrierLocalOffset;
            activeBarrierInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            Vector3 worldPosition = transform.position + transform.TransformVector(veilBarrierLocalOffset);
            activeBarrierInstance = Instantiate(barrierPrefab, worldPosition, transform.rotation);
        }

        activeBarrierInstance.transform.localScale = veilBarrierScale;
        Collider[] colliders = activeBarrierInstance.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }

    private void DestroyBarrierInstance()
    {
        if (activeBarrierInstance != null)
        {
            Destroy(activeBarrierInstance);
            activeBarrierInstance = null;
        }
    }

    private void ApplyBarrierTransform()
    {
        if (!IsDefending || activeBarrierInstance == null || !wFollowPlayer)
        {
            return;
        }

        activeBarrierInstance.transform.localPosition = veilBarrierLocalOffset;
        activeBarrierInstance.transform.localRotation = Quaternion.identity;
        activeBarrierInstance.transform.localScale = veilBarrierScale;
    }

    private void StartEnemyDebuffRoutine()
    {
        StopEnemyDebuffRoutine();
        enemyDebuffRoutine = StartCoroutine(EnemyDebuffRefreshLoop());
    }

    private void StopEnemyDebuffRoutine()
    {
        if (enemyDebuffRoutine != null)
        {
            StopCoroutine(enemyDebuffRoutine);
            enemyDebuffRoutine = null;
        }
    }

    private IEnumerator EnemyDebuffRefreshLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, enemyDebuffRefreshInterval));
        while (IsDefending)
        {
            RefreshEnemyDebuffs();
            yield return wait;
        }
    }

    private void RefreshEnemyDebuffs()
    {
        currentDebuffedEnemies.Clear();

        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, wRadius), ~0, QueryTriggerInteraction.Collide);
        if (debugLog)
        {
            Debug.Log($"[Player01 W Veil] overlap count = {hits.Length}", this);
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            EnemyController enemyController = ResolveEnemyController(hit);
            if (enemyController == null)
            {
                if (debugLog)
                {
                    Debug.Log($"[Player01 W Veil] collider has no EnemyController = {hit.name}", hit);
                }

                continue;
            }

            if (debugLog)
            {
                Debug.Log($"[Player01 W Veil] found enemy = {enemyController.name}", enemyController);
            }

            EnemyDebuffReceiver receiver = enemyController.GetComponentInParent<EnemyDebuffReceiver>();
            if (receiver == null)
            {
                receiver = enemyController.gameObject.AddComponent<EnemyDebuffReceiver>();
            }

            receiver.ApplyMoveSpeedMultiplier(damageModifierKey, Mathf.Clamp01(enemyMoveSpeedMultiplier));
            receiver.ApplyAttackMultiplier(damageModifierKey, Mathf.Clamp01(enemyAttackMultiplier));
            currentDebuffedEnemies.Add(receiver);

            if (debugLog)
            {
                Debug.Log($"[EnemyDebuff] Apply key={damageModifierKey} move={Mathf.Clamp01(enemyMoveSpeedMultiplier):F2} attack={Mathf.Clamp01(enemyAttackMultiplier):F2} enemy={enemyController.name}", enemyController);
            }

            if (!activeDebuffedEnemies.Contains(receiver) && debugLog)
            {
                Debug.Log($"[Player01 W Veil] Apply enemy debuff: {enemyController.name}", enemyController);
            }
        }

        RemoveLostEnemyDebuffs();

        activeDebuffedEnemies.Clear();
        foreach (EnemyDebuffReceiver receiver in currentDebuffedEnemies)
        {
            if (receiver != null)
            {
                activeDebuffedEnemies.Add(receiver);
            }
        }
    }

    private void RemoveLostEnemyDebuffs()
    {
        foreach (EnemyDebuffReceiver receiver in activeDebuffedEnemies)
        {
            if (receiver == null || currentDebuffedEnemies.Contains(receiver))
            {
                continue;
            }

            receiver.RemoveMoveSpeedMultiplier(damageModifierKey);
            receiver.RemoveAttackMultiplier(damageModifierKey);

            if (debugLog)
            {
                Debug.Log($"[EnemyDebuff] Remove key={damageModifierKey} enemy={receiver.gameObject.name}", receiver);
            }
        }
    }

    private void ClearEnemyDebuffs()
    {
        foreach (EnemyDebuffReceiver receiver in activeDebuffedEnemies)
        {
            if (receiver == null)
            {
                continue;
            }

            receiver.RemoveMoveSpeedMultiplier(damageModifierKey);
            receiver.RemoveAttackMultiplier(damageModifierKey);
        }

        activeDebuffedEnemies.Clear();
        currentDebuffedEnemies.Clear();
    }

    private GameObject ResolveBarrierPrefab()
    {
        if (veilBarrierPrefab != null)
        {
            return veilBarrierPrefab;
        }

        if (shieldPrefab != null)
        {
            return shieldPrefab;
        }

        return null;
    }

    private EnemyController ResolveEnemyController(Collider hit)
    {
        if (hit == null)
        {
            return null;
        }

        EnemyController enemyController = hit.GetComponentInParent<EnemyController>();
        if (enemyController != null)
        {
            return enemyController;
        }

        if (hit.attachedRigidbody != null)
        {
            enemyController = hit.attachedRigidbody.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                return enemyController;
            }
        }

        return hit.transform.root != null ? hit.transform.root.GetComponentInChildren<EnemyController>() : null;
    }
}
