using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player2Skill_Q_DivineLightSword : PlayerSkillBase
{
    [Header("Q - Divine Light Sword / Basic")]
    [InspectorName("Q Delay")]
    [SerializeField] private float qDelay = 0.35f;

    [Header("Q - Divine Light Sword / Basic")]
    [InspectorName("Q Skill Effect Prefab")]
    [SerializeField] private GameObject qSkillEffectPrefab;

    [Header("Q - Divine Light Sword / Fall Trail")]
    [HideInInspector]
    [InspectorName("Q Spawn Fall Trail")]
    [SerializeField] private bool qSpawnFallTrail = false;
    [HideInInspector]
    [InspectorName("Q Fall Trail Prefab")]
    [SerializeField] private GameObject qFallTrailPrefab;
    [HideInInspector]
    [InspectorName("Q Fall Trail Local Offset")]
    [SerializeField] private Vector3 qFallTrailLocalOffset = Vector3.zero;
    [HideInInspector]
    [InspectorName("Q Fall Trail Scale")]
    [SerializeField] private Vector3 qFallTrailScale = Vector3.one;

    [Header("Q - Divine Light Sword / Legacy Cone Spark")]
    [HideInInspector]
    [InspectorName("Q Spawn Impact Cone Spark")]
    [SerializeField] private bool qSpawnImpactConeSpark = true;
    [HideInInspector]
    [InspectorName("Q Impact Cone Spark Prefab")]
    [SerializeField] private GameObject qImpactConeSparkPrefab;
    [HideInInspector]
    [InspectorName("Q Impact Cone Spark Lifetime")]
    [SerializeField] private float qImpactConeSparkLifetime = 0.6f;
    [HideInInspector]
    [InspectorName("Q Impact Cone Spark Scale")]
    [SerializeField] private Vector3 qImpactConeSparkScale = Vector3.one;

    [Header("Q - Divine Light Sword / Impact Dust")]
    [InspectorName("Q Spawn Impact Dust")]
    [SerializeField] private bool qSpawnImpactDust = true;
    [InspectorName("Q Impact Dust Prefab")]
    [SerializeField] private GameObject qImpactDustPrefab;
    [InspectorName("Q Impact Dust Lifetime")]
    [SerializeField] private float qImpactDustLifetime = 1f;
    [InspectorName("Q Impact Dust Local Offset")]
    [SerializeField] private Vector3 qImpactDustLocalOffset = new Vector3(0f, 0.2f, 0f);
    [InspectorName("Q Impact Dust Scale")]
    [SerializeField] private Vector3 qImpactDustScale = Vector3.one;
    [InspectorName("Q Show Dust Debug Marker")]
    [SerializeField] private bool qShowDustDebugMarker = false;

    [Header("Q - Divine Light Sword / Star Fall")]
    [InspectorName("Q Star Fall Blade Count")]
    [SerializeField] private int qStarFallBladeCount = 7;
    [InspectorName("Q Star Fall Path Length")]
    [SerializeField] private float qStarFallPathLength = 4.5f;
    [InspectorName("Q Star Fall Start Offset")]
    [SerializeField] private float qStarFallStartOffset = 0.8f;
    [InspectorName("Q Star Fall Forward Jitter")]
    [SerializeField] private float qStarFallForwardJitter = 0.35f;
    [InspectorName("Q Star Fall Side Jitter")]
    [SerializeField] private float qStarFallSideJitter = 0.8f;
    [InspectorName("Q Star Fall Spread Grow Along Path")]
    [SerializeField] private bool qStarFallSpreadGrowAlongPath = true;
    [InspectorName("Q Star Fall Spread Grow Multiplier")]
    [SerializeField] private float qStarFallSpreadGrowMultiplier = 1.2f;
    [InspectorName("Q 七星剑反向释放")]
    [SerializeField] private bool qStarFallUseOppositeDirection = true;
    [InspectorName("Q Star Fall Spawn Height")]
    [SerializeField] private float qStarFallSpawnHeight = 5f;
    [InspectorName("Q Star Fall Fall Speed")]
    [SerializeField] private float qStarFallFallSpeed = 12f;
    [InspectorName("Q Star Fall Sequential Delay")]
    [SerializeField] private float qStarFallSequentialDelay = 0.06f;
    [InspectorName("Q Star Fall Path Jitter")]
    [SerializeField] private float qStarFallPathJitter = 0.15f;

    [Header("Q - Divine Light Sword / Damage")]
    [InspectorName("Q Star Fall Damage Radius")]
    [SerializeField] private float qStarFallDamageRadius = 0.8f;
    [InspectorName("Q Star Fall Enable Damage")]
    [SerializeField] private bool qStarFallEnableDamage = false;
    [InspectorName("Q Star Fall Damage Multiplier")]
    [SerializeField] private float qStarFallDamageMultiplier = 1f;

    [Header("Q - Divine Light Sword / Impact")]
    [HideInInspector]
    [SerializeField] private bool qSpawnImpactSeal = false;
    [HideInInspector]
    [SerializeField] private GameObject qImpactStarSealPrefab;
    [HideInInspector]
    [SerializeField] private float qImpactStarSealLifetime = 0.45f;
    [HideInInspector]
    [SerializeField] private Vector3 qImpactStarSealScale = Vector3.one;
    [HideInInspector]
    [SerializeField] private bool qSpawnVerticalFlash = false;
    [HideInInspector]
    [SerializeField] private GameObject qImpactVerticalFlashPrefab;
    [HideInInspector]
    [SerializeField] private float qImpactVerticalFlashLifetime = 0.18f;
    [HideInInspector]
    [SerializeField] private Vector3 qImpactVerticalFlashScale = Vector3.one;

    [HideInInspector]
    [SerializeField] private bool qSpawnImpactParticle = false;
    [HideInInspector]
    [SerializeField] private GameObject qImpactParticlePrefab;
    [HideInInspector]
    [SerializeField] private float qImpactParticleLifetime = 1f;
    [HideInInspector]
    [SerializeField] private Vector3 qImpactParticleScale = Vector3.one;

    [Header("Q - Divine Light Sword / Visual")]
    [InspectorName("Q Star Fall Use Forced Visual Rotation")]
    [SerializeField] private bool qStarFallUseForcedVisualRotation = true;
    [InspectorName("Q Star Fall Forced Visual Euler")]
    [SerializeField] private Vector3 qStarFallForcedVisualEuler = new Vector3(90f, 90f, 90f);
    [InspectorName("Q Star Fall Visual Euler Offset")]
    [SerializeField] private Vector3 qStarFallVisualEulerOffset = Vector3.zero;
    [InspectorName("Q Star Fall Face Fall Direction")]
    [SerializeField] private bool qStarFallFaceFallDirection = false;
    [InspectorName("Q Star Fall Effect Scale")]
    [SerializeField] private Vector3 qStarFallEffectScale = new Vector3(0.35f, 0.35f, 0.35f);
    [InspectorName("Q 最大同时存在波数")]
    [SerializeField] private int qMaxActiveWaves = 3;

    [Header("Q - Divine Light Sword / Legacy")]
    [HideInInspector]
    [SerializeField] private float qSwordSpeed = 14f;
    [HideInInspector]
    [SerializeField] private Vector3 qEffectScale = new Vector3(0.25f, 0.25f, 0.25f);
    [HideInInspector]
    [SerializeField] private float qEffectRotationZ = 0f;
    [HideInInspector]
    [SerializeField] private Vector3 qEffectOffset = Vector3.zero;
    [HideInInspector]
    [SerializeField] private Vector3 qEffectPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    [HideInInspector]
    [SerializeField] private float qEffectYawOffset = 0f;
    [HideInInspector]
    [SerializeField] private float qEffectVisualPitch = 0f;
    [HideInInspector]
    [SerializeField] private float qEffectVisualYaw = 0f;
    [HideInInspector]
    [SerializeField] private float qEffectVisualRoll = 0f;
    [HideInInspector]
    [SerializeField] private bool qEffectInvertForward = false;
    [HideInInspector]
    [SerializeField] private bool qAutoTrackEnemy = true;
    [HideInInspector]
    [SerializeField] private float qHomingSearchRadius = 18f;
    [HideInInspector]
    [SerializeField] private float qHomingTurnSpeed = 540f;
    [HideInInspector]
    [SerializeField] private bool qKeepPaperFlat = true;
    [HideInInspector]
    [SerializeField] private float qSpreadAngle = 10f;
    [HideInInspector]
    [SerializeField] private float qSpawnSideOffsetRandom = 0.35f;
    [HideInInspector]
    [SerializeField] private float qWaveAmplitude = 0.22f;
    [HideInInspector]
    [SerializeField] private float qWaveFrequency = 1.6f;
    [HideInInspector]
    [SerializeField] private float qArcHeight = 0.18f;
    [HideInInspector]
    [SerializeField] private float qProjectileLife = 2.2f;
    [HideInInspector]
    [SerializeField] private bool qRotateAlongVelocity = true;
    [HideInInspector]
    [SerializeField] private float qVisualPitchJitter = 12f;
    [HideInInspector]
    [SerializeField] private float qVisualYawJitter = 10f;

    private readonly List<GameObject> activeQBlades = new List<GameObject>();
    private int activeQWaveCount;
    private Coroutine qCastRoutine;

    public override void Initialize(Player2PrototypeController owner)
    {
        base.Initialize(owner);
    }

    public override void Cast()
    {
        if (Owner == null)
        {
            return;
        }

        GameObject sourcePrefab = ResolveQVisualPrefab();
        if (sourcePrefab == null)
        {
            Debug.LogWarning("[Player2Skill_Q_DivineLightSword] Missing Q Skill Effect Prefab and Shared Skill Effect Prefab.", this);
            return;
        }

        int maxActiveWaves = Mathf.Max(1, qMaxActiveWaves);
        if (activeQWaveCount >= maxActiveWaves)
        {
            Debug.LogWarning($"[Player2Skill_Q_DivineLightSword] Reached max active Q waves ({maxActiveWaves}). New Q cast ignored.", this);
            return;
        }

        Owner.currentSwordEnergy += 1;
        qCastRoutine = StartCoroutine(QStarFallRoutine(sourcePrefab));
        Owner.GetComponentInChildren<Player2HaloRotateEffect>(true)?.TriggerSkillBoost();
    }

    public override void Cleanup()
    {
        StopAllCoroutines();
        qCastRoutine = null;
        activeQWaveCount = 0;

        for (int i = 0; i < activeQBlades.Count; i++)
        {
            if (activeQBlades[i] != null)
            {
                Destroy(activeQBlades[i]);
            }
        }

        activeQBlades.Clear();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (qImpactDustPrefab == null)
        {
            qImpactDustPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/QImpactDust_Visible.prefab");
        }
#endif
    }

    private IEnumerator QStarFallRoutine(GameObject sourcePrefab)
    {
        activeQWaveCount++;
        try
        {
            List<GameObject> waveBlades = new List<GameObject>();

            if (qDelay > 0f)
            {
                yield return new WaitForSeconds(qDelay);
            }

            if (Owner == null)
            {
                yield break;
            }

            Vector3 startPos = Owner.transform.position;
            Vector3 castDir = ResolveCastDirection();
            Vector3 pathStart = startPos + castDir * Mathf.Max(0f, qStarFallStartOffset);
            Vector3 endPos = pathStart + castDir * Mathf.Max(0.01f, qStarFallPathLength);
            int bladeCount = Mathf.Max(1, qStarFallBladeCount);
            Vector3 sideDir = Vector3.Cross(Vector3.up, castDir);
            if (sideDir.sqrMagnitude < 0.0001f)
            {
                sideDir = Owner.transform.right;
            }
            if (sideDir.sqrMagnitude < 0.0001f)
            {
                sideDir = Vector3.right;
            }
            sideDir.Normalize();

            for (int i = 0; i < bladeCount; i++)
            {
                if (Owner == null)
                {
                    yield break;
                }

                float t = bladeCount <= 1 ? 0f : i / (float)(bladeCount - 1);
                Vector3 basePos = Vector3.Lerp(pathStart, endPos, t);

                float grow = 1f;
                if (qStarFallSpreadGrowAlongPath)
                {
                    grow = Mathf.Lerp(1f, Mathf.Max(1f, qStarFallSpreadGrowMultiplier), t);
                }

                float forwardOffset = Random.Range(-Mathf.Abs(qStarFallForwardJitter), Mathf.Abs(qStarFallForwardJitter)) * grow;
                float sideOffset = Random.Range(-Mathf.Abs(qStarFallSideJitter), Mathf.Abs(qStarFallSideJitter)) * grow;
                Vector3 targetPos = basePos + castDir * forwardOffset + sideDir * sideOffset;

                GameObject blade = SpawnQStarFallBlade(sourcePrefab, targetPos);
                if (blade != null)
                {
                    waveBlades.Add(blade);
                    activeQBlades.Add(blade);
                    StartCoroutine(QStarFallBladeRoutine(blade, waveBlades, targetPos));
                }

                if (qStarFallSequentialDelay > 0f)
                {
                    yield return new WaitForSeconds(qStarFallSequentialDelay);
                }
                else
                {
                    yield return null;
                }
            }
        }
        finally
        {
            activeQWaveCount = Mathf.Max(0, activeQWaveCount - 1);
            qCastRoutine = null;
        }
    }

    private GameObject SpawnQStarFallBlade(GameObject sourcePrefab, Vector3 targetPos)
    {
        if (sourcePrefab == null || Owner == null)
        {
            return null;
        }

        Vector3 spawnPos = targetPos + Vector3.up * Mathf.Max(0.1f, qStarFallSpawnHeight);
        Vector3 fallDirection = (targetPos - spawnPos).sqrMagnitude > 0.0001f ? (targetPos - spawnPos).normalized : Vector3.down;

        GameObject bladeRoot = new GameObject("Q_StarFallBlade");
        bladeRoot.transform.position = spawnPos;

        GameObject bladeVisual = Instantiate(sourcePrefab);
        bladeVisual.transform.position = spawnPos;
        bladeVisual.transform.SetParent(bladeRoot.transform, true);

        Transform visualTarget = FindEffectVisualTransform(bladeVisual);
        ApplyQStarFallVisualRotation(visualTarget, fallDirection);
        visualTarget.localScale = Vector3.Scale(visualTarget.localScale, ClampVisualScale(qStarFallEffectScale));

        return bladeRoot;
    }

    private IEnumerator QStarFallBladeRoutine(GameObject bladeRoot, List<GameObject> waveBlades, Vector3 targetPos)
    {
        while (bladeRoot != null && Vector3.Distance(bladeRoot.transform.position, targetPos) > 0.05f)
        {
            bladeRoot.transform.position = Vector3.MoveTowards(
                bladeRoot.transform.position,
                targetPos,
                Mathf.Max(0.01f, qStarFallFallSpeed) * Time.deltaTime);
            yield return null;
        }

        if (bladeRoot != null)
        {
            bladeRoot.transform.position = targetPos;
            if (qStarFallEnableDamage)
            {
                ApplyQStarFallDamage(targetPos);
            }

            SpawnQImpactDust(targetPos);
            activeQBlades.Remove(bladeRoot);
            if (waveBlades != null)
            {
                waveBlades.Remove(bladeRoot);
            }
            Destroy(bladeRoot, 0.05f);
        }
    }

    private void ApplyQStarFallDamage(Vector3 center)
    {
        if (!qStarFallEnableDamage || qStarFallDamageRadius <= 0f)
        {
            return;
        }

        float damageAmount = Mathf.Max(0f, qStarFallDamageMultiplier);
        if (damageAmount <= 0f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(center, qStarFallDamageRadius);
        HashSet<GameObject> damagedRoots = new HashSet<GameObject>();
        GameObject source = Owner != null ? Owner.gameObject : gameObject;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Transform targetRoot = hit.transform.root;
            if (targetRoot == null || (Owner != null && targetRoot.gameObject == Owner.gameObject) || !damagedRoots.Add(targetRoot.gameObject))
            {
                continue;
            }

            CombatHealth combatHealth = targetRoot.GetComponentInParent<CombatHealth>();
            if (combatHealth != null && (Owner == null || combatHealth.gameObject != Owner.gameObject))
            {
                combatHealth.TakeDamage(new BattleDamage(damageAmount, BattleDamageType.Physical, source));
                continue;
            }

            EnemyHealth enemyHealth = targetRoot.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null && (Owner == null || enemyHealth.gameObject != Owner.gameObject))
            {
                int damageInt = Mathf.Max(1, Mathf.RoundToInt(damageAmount));
                enemyHealth.TakeDamage(damageInt, source);
            }
        }
    }

    private void SpawnQImpactDust(Vector3 impactPosition)
    {
#if UNITY_EDITOR
        if (qImpactDustPrefab == null)
        {
            qImpactDustPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/QImpactDust_Visible.prefab");
        }
#endif
        if (!qSpawnImpactDust || qImpactDustPrefab == null)
        {
            return;
        }

        Vector3 spawnPos = impactPosition + qImpactDustLocalOffset;
        Debug.Log($"[Q Dust] spawn requested, prefab={qImpactDustPrefab.name}, qSpawnImpactDust={qSpawnImpactDust}", this);
        Debug.Log($"[Q Dust] impactPosition={impactPosition}, localOffset={qImpactDustLocalOffset}, finalPosition={spawnPos}", this);

        GameObject impactObject = Instantiate(qImpactDustPrefab, spawnPos, Quaternion.identity);
        impactObject.SetActive(true);
        impactObject.transform.localScale = ClampVisualScale(qImpactDustScale);
        Debug.Log($"[Q Dust] instantiated name={impactObject.name}, activeSelf={impactObject.activeSelf}, activeInHierarchy={impactObject.activeInHierarchy}", this);

        ParticleSystem[] particleSystems = impactObject.GetComponentsInChildren<ParticleSystem>(true);
        int particleSystemCount = particleSystems != null ? particleSystems.Length : 0;
        Debug.Log($"[Q Dust] particleSystems count={particleSystemCount}", this);
        if (particleSystems == null || particleSystems.Length == 0)
        {
            Debug.LogWarning($"[Q Dust] spawned prefab without ParticleSystem: {qImpactDustPrefab.name}", this);
        }
        else
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                ParticleSystem.MinMaxGradient startColor = main.startColor;
                ParticleSystem.MinMaxCurve startSize = main.startSize;
                ParticleSystem.MinMaxCurve startLifetime = main.startLifetime;
                ParticleSystem.EmissionModule emission = particleSystem.emission;
                int burstCount = 0;
                try
                {
                    ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[32];
                    burstCount = emission.GetBursts(bursts);
                }
                catch (System.Exception)
                {
                    burstCount = -1;
                }

                if (startColor.mode == ParticleSystemGradientMode.Color && startColor.color.a < 0.25f)
                {
                    Debug.LogWarning($"[Q Dust] particle start alpha is low: {particleSystem.name}, alpha={startColor.color.a}", this);
                }

                ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                string psName = particleSystem.name;
                string startColorText = startColor.mode == ParticleSystemGradientMode.Color ? startColor.color.ToString() : startColor.mode.ToString();
                string startSizeText = startSize.mode.ToString();
                string startLifetimeText = startLifetime.mode.ToString();
                string rendererName = particleRenderer != null ? particleRenderer.name : "null";
                string sharedMaterialName = particleRenderer != null && particleRenderer.sharedMaterial != null ? particleRenderer.sharedMaterial.name : "null";
                string renderModeText = particleRenderer != null ? particleRenderer.renderMode.ToString() : "null";
                int sortingOrder = particleRenderer != null ? particleRenderer.sortingOrder : -1;
                int renderQueue = particleRenderer != null && particleRenderer.sharedMaterial != null ? particleRenderer.sharedMaterial.renderQueue : -1;

                Debug.Log(
                    "[Q Dust] ps name=" + psName
                    + ", startColor=" + startColorText
                    + ", startSize=" + startSizeText
                    + ", startLifetime=" + startLifetimeText
                    + ", emission.enabled=" + emission.enabled
                    + ", burst count=" + burstCount
                    + ", isPlaying=" + particleSystem.isPlaying,
                    this);

                Debug.Log(
                    "[Q Dust] psRenderer name=" + rendererName
                    + ", enabled=" + (particleRenderer != null && particleRenderer.enabled)
                    + ", sharedMaterial=" + sharedMaterialName
                    + ", renderMode=" + renderModeText
                    + ", sortingOrder=" + sortingOrder
                    + ", renderQueue=" + renderQueue,
                    this);

                particleSystem.Clear(true);
                particleSystem.Play(true);
                Debug.Log($"[Q Dust] Play called on {particleSystem.name}", this);
            }
        }

        Renderer[] renderers = impactObject.GetComponentsInChildren<Renderer>(true);
        int rendererCount = renderers != null ? renderers.Length : 0;
        Debug.Log($"[Q Dust] renderers count={rendererCount}", this);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[Q Dust] spawned prefab without Renderer: {qImpactDustPrefab.name}", this);
        }
        else
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material rendererMaterial = renderer.sharedMaterial != null ? renderer.sharedMaterial : renderer.material;
                string rendererMaterialName = rendererMaterial != null ? rendererMaterial.name : "null";
                int rendererRenderQueue = rendererMaterial != null ? rendererMaterial.renderQueue : -1;
                Debug.Log(
                    "[Q Dust] renderer enabled=" + renderer.enabled
                    + ", material=" + rendererMaterialName
                    + ", sortingOrder=" + renderer.sortingOrder
                    + ", renderQueue=" + rendererRenderQueue,
                    this);
                if (rendererMaterial == null)
                {
                    Debug.LogWarning($"[Q Dust] renderer missing material: {renderer.name}", this);
                }
            }
        }

        Debug.Log($"[Q Dust] spawned at {spawnPos}", this);

        if (qShowDustDebugMarker)
        {
            GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            debugSphere.name = "Q_Dust_DebugMarker";
            debugSphere.transform.position = spawnPos;
            debugSphere.transform.localScale = Vector3.one * 0.12f;
            Renderer debugSphereRenderer = debugSphere.GetComponent<Renderer>();
            if (debugSphereRenderer != null)
            {
                debugSphereRenderer.material.color = new Color(1f, 1f, 0f, 1f);
                debugSphereRenderer.sortingOrder = 200;
            }
            Collider debugSphereCollider = debugSphere.GetComponent<Collider>();
            if (debugSphereCollider != null)
            {
                Destroy(debugSphereCollider);
            }
            Destroy(debugSphere, 1f);
        }

        Destroy(impactObject, Mathf.Max(0.05f, qImpactDustLifetime));
    }

    private Vector3 ResolveCastDirection()
    {
        Vector3 castDir = Owner != null ? Owner.GetFacingDirection() : Vector3.forward;
        castDir.y = 0f;
        if (castDir.sqrMagnitude < 0.0001f)
        {
            castDir = Owner != null ? Owner.transform.forward : Vector3.forward;
            castDir.y = 0f;
        }

        if (castDir.sqrMagnitude < 0.0001f)
        {
            castDir = Vector3.forward;
        }

        castDir.Normalize();
        if (qStarFallUseOppositeDirection)
        {
            castDir = -castDir;
        }
        return castDir;
    }

    private GameObject ResolveQVisualPrefab()
    {
        if (qSkillEffectPrefab != null)
        {
            return qSkillEffectPrefab;
        }

        return Owner != null ? Owner.GetSharedSkillEffectPrefab() : null;
    }

    private void ApplyQStarFallVisualRotation(Transform visualTarget, Vector3 fallDirection)
    {
        if (visualTarget == null)
        {
            return;
        }

        Quaternion rotation;
        if (qStarFallUseForcedVisualRotation)
        {
            rotation = Quaternion.Euler(qStarFallForcedVisualEuler + qStarFallVisualEulerOffset);
        }
        else if (qStarFallFaceFallDirection)
        {
            Vector3 safeFallDirection = fallDirection.sqrMagnitude > 0.0001f ? fallDirection.normalized : Vector3.down;
            rotation = Quaternion.LookRotation(safeFallDirection, Vector3.up) * Quaternion.Euler(qStarFallVisualEulerOffset);
        }
        else
        {
            rotation = Quaternion.Euler(qStarFallVisualEulerOffset);
        }

        visualTarget.localRotation = rotation;
    }

    private static Transform FindEffectVisualTransform(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        MeshRenderer rootMesh = root.GetComponent<MeshRenderer>();
        if (rootMesh != null)
        {
            return root.transform;
        }

        MeshRenderer childMesh = root.GetComponentInChildren<MeshRenderer>(true);
        if (childMesh != null)
        {
            return childMesh.transform;
        }

        SpriteRenderer rootSprite = root.GetComponent<SpriteRenderer>();
        if (rootSprite != null)
        {
            return root.transform;
        }

        SpriteRenderer childSprite = root.GetComponentInChildren<SpriteRenderer>(true);
        if (childSprite != null)
        {
            return childSprite.transform;
        }

        return root.transform;
    }

    private static Vector3 ClampVisualScale(Vector3 scale)
    {
        return new Vector3(
            ClampScaleAxis(scale.x),
            ClampScaleAxis(scale.y),
            ClampScaleAxis(scale.z));
    }

    private static float ClampScaleAxis(float value)
    {
        const float minAbs = 0.01f;
        if (Mathf.Abs(value) >= minAbs)
        {
            return value;
        }

        return value < 0f ? -minAbs : minAbs;
    }
}

