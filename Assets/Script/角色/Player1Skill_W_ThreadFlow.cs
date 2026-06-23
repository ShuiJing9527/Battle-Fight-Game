using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player1Skill_W_ThreadFlow : Player01SkillBase
{
    [Header("W - Veil Barrier / Base")]
    [SerializeField, Min(0f)] private float wDuration = 8f;
    [SerializeField, Min(0f)] private float wCooldown = 8f;
    [SerializeField, Min(0.1f)] private float wRadius = 4.5f;
    [SerializeField] private bool wFollowPlayer = true;

    [Header("W - Veil Barrier / Damage")]
    [SerializeField, Range(0f, 1f)] private float playerDamageTakenMultiplier = 0.5f;

    [Header("W - Veil Barrier / Enemy Debuff")]
    [SerializeField, Range(0f, 1f)] private float enemyMoveSpeedMultiplier = 0.5f;
    [SerializeField, Range(0f, 1f)] private float enemyAttackMultiplier = 0.5f;
    [SerializeField, Min(0.01f)] private float enemyDebuffRefreshInterval = 0.1f;

    [Header("W - Veil Barrier / Visual")]
    [SerializeField] private GameObject veilBarrierPrefab;
    [SerializeField] private Material veilBarrierMaterial;
    [SerializeField] private Vector3 veilBarrierScale = new Vector3(45f, 45f, 45f);
    [SerializeField] private Vector3 veilBarrierLocalOffset = Vector3.zero;

    [Header("W - Veil Barrier / Dissolve")]
    [SerializeField, Min(0.01f)] private float wDissolveInDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float wDissolveOutDuration = 0.45f;
    [SerializeField, Range(0f, 1f)] private float wDissolveStartValue = 1f;
    [SerializeField, Range(0f, 1f)] private float wDissolveVisibleValue = 0f;
    [SerializeField, Range(0f, 1f)] private float wDissolveHiddenValue = 1f;
    [SerializeField, Min(0f)] private float wEdgeIntensityNormal = 2.5f;
    [SerializeField, Min(0f)] private float wEdgeIntensityBurst = 3.5f;

    [Header("Legacy")]
    [SerializeField, HideInInspector, Min(0f)] private float damageReduction = 0.4f;
    [SerializeField, HideInInspector] private GameObject shieldPrefab;

    public bool IsDefending { get; private set; }

    private readonly HashSet<EnemyDebuffReceiver> activeDebuffedEnemies = new HashSet<EnemyDebuffReceiver>();
    private readonly HashSet<EnemyDebuffReceiver> currentDebuffedEnemies = new HashSet<EnemyDebuffReceiver>();
    private CombatHealth cachedCombatHealth;
    private GameObject activeBarrierInstance;
    private Renderer[] activeBarrierRenderers;
    private GameObject dissolveOverlayInstance;
    private Renderer[] dissolveOverlayRenderers;
    private Material barrierMaterialInstance;
    private Coroutine barrierDissolveRoutine;
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
        wDissolveInDuration = 0.4f;
        wDissolveOutDuration = 0.45f;
        wDissolveStartValue = 1f;
        wDissolveVisibleValue = 0f;
        wDissolveHiddenValue = 1f;
        wEdgeIntensityNormal = 2.5f;
        wEdgeIntensityBurst = 3.5f;
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
        StartBarrierDissolveIn();
        Controller?.FinishSkill(this);

        if (debugLog)
        {
            Debug.Log("[Player01 W Veil] barrier active, skills remain usable", this);
            Debug.Log($"[Player01 W Veil] Start duration={wDuration:F2} radius={wRadius:F2}", this);
        }
    }

    protected override IEnumerator CastRoutine()
    {
        float waitTime = Mathf.Max(0f, duration);
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
        else
        {
            yield return null;
        }

        OnCastFinished();
        CompleteCast();
    }

    protected override void OnCastFinished()
    {
        IsDefending = false;
        StopEnemyDebuffRoutine();
        RemovePlayerDamageModifier();
        ClearEnemyDebuffs();
        StartBarrierDissolveOut();

        if (debugLog)
        {
            Debug.Log("[Player01 W Veil] End", this);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        CleanupBarrierVisualImmediate();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CleanupBarrierVisualImmediate();
    }

    protected override string GetSkillLabel()
    {
        return "W - Veil Barrier";
    }

    protected override int SkillIndex => 1;

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

    private void ApplyBarrierTransform()
    {
        if (activeBarrierInstance == null)
        {
            return;
        }

        Transform followTarget = transform;

        if (wFollowPlayer)
        {
            activeBarrierInstance.transform.SetParent(followTarget, false);
            activeBarrierInstance.transform.localPosition = veilBarrierLocalOffset;
            activeBarrierInstance.transform.localRotation = Quaternion.identity;
            activeBarrierInstance.transform.localScale = veilBarrierScale;

            if (dissolveOverlayInstance != null)
            {
                dissolveOverlayInstance.transform.SetParent(followTarget, false);
                dissolveOverlayInstance.transform.localPosition = veilBarrierLocalOffset;
                dissolveOverlayInstance.transform.localRotation = Quaternion.identity;
                dissolveOverlayInstance.transform.localScale = veilBarrierScale;
            }
            return;
        }

        activeBarrierInstance.transform.SetParent(null, true);
        activeBarrierInstance.transform.position = followTarget.position + followTarget.TransformVector(veilBarrierLocalOffset);
        activeBarrierInstance.transform.rotation = Quaternion.identity;
        activeBarrierInstance.transform.localScale = veilBarrierScale;

        if (dissolveOverlayInstance != null)
        {
            dissolveOverlayInstance.transform.SetParent(null, true);
            dissolveOverlayInstance.transform.position = followTarget.position + followTarget.TransformVector(veilBarrierLocalOffset);
            dissolveOverlayInstance.transform.rotation = Quaternion.identity;
            dissolveOverlayInstance.transform.localScale = veilBarrierScale;
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
        CleanupBarrierVisualImmediate();

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
        activeBarrierRenderers = activeBarrierInstance.GetComponentsInChildren<Renderer>(true);
        SetRenderersEnabled(activeBarrierRenderers, false);
        CreateDissolveOverlayInstance();
        ApplyBarrierMaterialState(wDissolveStartValue, 0f, wEdgeIntensityBurst);

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
            activeBarrierRenderers = null;
        }
    }

    private void DestroyDissolveOverlayInstance()
    {
        if (dissolveOverlayInstance != null)
        {
            Destroy(dissolveOverlayInstance);
            dissolveOverlayInstance = null;
            dissolveOverlayRenderers = null;
        }
    }

    private void DestroyBarrierMaterialInstance()
    {
        if (barrierMaterialInstance != null)
        {
            Destroy(barrierMaterialInstance);
            barrierMaterialInstance = null;
        }
    }

    private void CleanupBarrierVisualImmediate()
    {
        StopBarrierDissolveRoutine();
        DestroyDissolveOverlayInstance();
        DestroyBarrierInstance();
        DestroyBarrierMaterialInstance();
    }

    private void ApplyBarrierMaterialInstance()
    {
        if (dissolveOverlayInstance == null)
        {
            return;
        }

        if (dissolveOverlayRenderers == null || dissolveOverlayRenderers.Length == 0)
        {
            dissolveOverlayRenderers = dissolveOverlayInstance.GetComponentsInChildren<Renderer>(true);
        }

        if (dissolveOverlayRenderers == null || dissolveOverlayRenderers.Length == 0)
        {
            return;
        }

        Material sourceMaterial = veilBarrierMaterial;
        if (sourceMaterial == null)
        {
            for (int i = 0; i < dissolveOverlayRenderers.Length; i++)
            {
                Renderer renderer = dissolveOverlayRenderers[i];
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    sourceMaterial = renderer.sharedMaterial;
                    break;
                }
            }
        }

        if (sourceMaterial == null)
        {
            if (debugLog)
            {
                Debug.LogWarning("[Player01 W Veil] Barrier material is missing.", this);
            }

            return;
        }

        DestroyBarrierMaterialInstance();
        barrierMaterialInstance = Instantiate(sourceMaterial);
        barrierMaterialInstance.name = $"{sourceMaterial.name} (Player01 W Veil Instance)";

        for (int i = 0; i < dissolveOverlayRenderers.Length; i++)
        {
            Renderer renderer = dissolveOverlayRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.material = barrierMaterialInstance;
        }
    }

    private void ApplyBarrierMaterialState(float dissolveAmount, float opacityMultiplier, float edgeIntensity)
    {
        if (barrierMaterialInstance == null)
        {
            return;
        }

        SetFloatIfExists(barrierMaterialInstance, "_DissolveAmount", dissolveAmount);
        SetFloatIfExists(barrierMaterialInstance, "_OpacityMultiplier", opacityMultiplier);
        SetFloatIfExists(barrierMaterialInstance, "_DissolveEdgeIntensity", edgeIntensity);
        SetFloatIfExists(barrierMaterialInstance, "_DissolveWidth", 0.06f);
        SetFloatIfExists(barrierMaterialInstance, "_DissolveSoftness", 0.02f);
    }

    private void StartBarrierDissolveIn()
    {
        if (dissolveOverlayInstance == null)
        {
            return;
        }

        StopBarrierDissolveRoutine();
        barrierDissolveRoutine = StartCoroutine(BarrierDissolveInRoutine());
    }

    private void StartBarrierDissolveOut()
    {
        StopBarrierDissolveRoutine();

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            CleanupBarrierVisualImmediate();
            return;
        }

        if (dissolveOverlayInstance == null && activeBarrierInstance == null)
        {
            DestroyBarrierMaterialInstance();
            return;
        }

        if (dissolveOverlayInstance == null && activeBarrierInstance != null)
        {
            CreateDissolveOverlayInstance();
        }

        barrierDissolveRoutine = StartCoroutine(BarrierDissolveOutRoutine());
    }

    private void StopBarrierDissolveRoutine()
    {
        if (barrierDissolveRoutine != null)
        {
            StopCoroutine(barrierDissolveRoutine);
            barrierDissolveRoutine = null;
        }
    }

    private IEnumerator BarrierDissolveInRoutine()
    {
        if (debugLog)
        {
            Debug.Log("[Player01 W Veil] Dissolve In Start", this);
        }

        ApplyBarrierMaterialState(wDissolveStartValue, 0f, wEdgeIntensityBurst);

        float durationSeconds = Mathf.Max(0.01f, wDissolveInDuration);
        float elapsed = 0f;
        while (elapsed < durationSeconds && activeBarrierInstance != null)
        {
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            float dissolveAmount = Mathf.Lerp(wDissolveStartValue, wDissolveVisibleValue, t);
            float opacityMultiplier = Mathf.Lerp(0f, 1f, t);
            float edgeIntensity = Mathf.Lerp(wEdgeIntensityBurst, wEdgeIntensityNormal, t);
            ApplyBarrierMaterialState(dissolveAmount, opacityMultiplier, edgeIntensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyBarrierMaterialState(wDissolveVisibleValue, 1f, wEdgeIntensityNormal);
        SetRenderersEnabled(activeBarrierRenderers, true);
        DestroyDissolveOverlayInstance();
        DestroyBarrierMaterialInstance();

        if (debugLog)
        {
            Debug.Log("[Player01 W Veil] Dissolve In End", this);
        }

        barrierDissolveRoutine = null;
    }

    private IEnumerator BarrierDissolveOutRoutine()
    {
        if (debugLog)
        {
            Debug.Log("[Player01 W Veil] Dissolve Out Start", this);
        }

        ApplyBarrierMaterialState(wDissolveVisibleValue, 1f, wEdgeIntensityNormal);
        SetRenderersEnabled(activeBarrierRenderers, false);

        float durationSeconds = Mathf.Max(0.01f, wDissolveOutDuration);
        float elapsed = 0f;
        while (elapsed < durationSeconds && activeBarrierInstance != null)
        {
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            float dissolveAmount = Mathf.Lerp(wDissolveVisibleValue, wDissolveHiddenValue, t);
            float opacityMultiplier = Mathf.Lerp(1f, 0f, t);
            float edgeIntensity = Mathf.Lerp(wEdgeIntensityNormal, wEdgeIntensityBurst, t);
            ApplyBarrierMaterialState(dissolveAmount, opacityMultiplier, edgeIntensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyBarrierMaterialState(wDissolveHiddenValue, 0f, wEdgeIntensityBurst);
        DestroyDissolveOverlayInstance();
        DestroyBarrierInstance();
        DestroyBarrierMaterialInstance();

        if (debugLog)
        {
            Debug.Log("[Player01 W Veil] Dissolve Out End", this);
        }

        barrierDissolveRoutine = null;
    }

    private void CreateDissolveOverlayInstance()
    {
        DestroyDissolveOverlayInstance();

        GameObject barrierPrefab = ResolveBarrierPrefab();
        if (barrierPrefab == null)
        {
            return;
        }

        if (wFollowPlayer)
        {
            dissolveOverlayInstance = Instantiate(barrierPrefab, transform);
            dissolveOverlayInstance.transform.localPosition = veilBarrierLocalOffset;
            dissolveOverlayInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            Vector3 worldPosition = transform.position + transform.TransformVector(veilBarrierLocalOffset);
            dissolveOverlayInstance = Instantiate(barrierPrefab, worldPosition, transform.rotation);
        }

        dissolveOverlayInstance.transform.localScale = veilBarrierScale;
        dissolveOverlayRenderers = dissolveOverlayInstance.GetComponentsInChildren<Renderer>(true);
        ApplyBarrierMaterialInstance();
        ApplyBarrierMaterialState(wDissolveStartValue, 0f, wEdgeIntensityBurst);

        Collider[] colliders = dissolveOverlayInstance.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }
    }

    private static void SetFloatIfExists(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
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
