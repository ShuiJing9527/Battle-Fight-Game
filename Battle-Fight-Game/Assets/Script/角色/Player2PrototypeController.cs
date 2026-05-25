using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class Player2PrototypeController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 5f;
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;
    [SerializeField] private bool lockCharacterRotation = true;

    [Header("Q - 神临光剑")]
    public float qDelay = 0.35f;
    public float qSwordSpeed = 14f;

    [Header("W - 圣轮偏转")]
    public float wDuration = 1.5f;
    public float wDamageReduction = 0.4f;
    public int maxStandbySwords = 3;
    [Header("W 防御加成")]
    [InspectorName("W 每把剑减伤加成")]
    public float wDamageReductionPerSword = 0.03f;
    [InspectorName("W 最大减伤")]
    public float wMaxDamageReduction = 0.8f;
    [InspectorName("W 反击伤害比例")]
    public float wCounterDamageRatio = 0.5f;

    [Header("E - 天轨换位")]
    public float eRailDuration = 0.6f;

    [Header("剑气值")]
    [InspectorName("当前剑气值")]
    public int currentSwordEnergy = 0;

    [Header("R - 万剑神罚")]
    [FormerlySerializedAs("swordEnergy")]
    [InspectorName("R 初始剑数量")]
    public int rBaseSwordCount = 1;

    [Header("Skill Effect Prefabs")]
    public GameObject sharedSkillEffectPrefab;
    public GameObject qSkillEffectPrefab;
    public GameObject wSkillEffectPrefab;
    public GameObject eSkillEffectPrefab;
    public GameObject rSkillEffectPrefab;
    public GameObject standbySkillEffectPrefab;

    [Header("Skill Effect Visuals - Shared")]
    public bool useRawPrefabRotationForSkillEffects = true;
    public Vector3 skillEffectPrefabBaseRotation = new Vector3(180.618f, 91.603f, -89.927f);
    public float skillEffectPrefabScaleMultiplier = 1f;
    public Vector3 sharedEffectScale = new Vector3(1f, 1f, 1f);
    public float sharedEffectRotationZ = 0f;

    [Header("Skill Effect Visuals - Q")]
    public Vector3 qEffectScale = new Vector3(0.25f, 0.25f, 0.25f);
    public float qEffectRotationZ = -90f;
    public Vector3 qEffectOffset = Vector3.zero;
    public Vector3 qEffectPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    public float qEffectYawOffset = 0f;
    public float qEffectVisualPitch = 0f;
    public float qEffectVisualYaw = 0f;
    public float qEffectVisualRoll = 0f;
    public bool qEffectInvertForward = false;

    [Header("Skill Effect Visuals - W")]
    public Vector3 wEffectScale = new Vector3(0.4f, 0.4f, 0.4f);
    public float wEffectRotationZ = 0f;
    public Vector3 wEffectOffset = Vector3.zero;
    public Vector3 wEffectPlaneScale = new Vector3(0.25f, 0.25f, 0.25f);
    [Tooltip("W 剑大小仅使用这个倍率。W Effect Scale / W Effect Plane Scale 不再参与 W 缩放。")]
    public float wEffectScaleMultiplier = 1f;
    public bool wEffectVerticalRotation = true;
    public Vector3 wEffectSpinAxis = Vector3.up;
    public float wEffectVisualPitch = 0f;
    public float wEffectVisualYaw = 0f;
    public float wEffectVisualRoll = 0f;

    [Header("W Orbit Settings")]
    public int wSwordCount = 3;
    [InspectorName("W 初始剑数量")]
    public int baseWSwordCount = 3;
    [InspectorName("W 使用剑气值")]
    public bool useSwordEnergyForW = true;
    [InspectorName("W 最大剑数量")]
    public int maxWSwordCount = 15;
    public float wEffectOrbitRadius = 1.2f;
    public float wEffectHeight = 1.1f;
    public float wEffectOrbitSpeed = 80f;
    public bool wEffectFaceCamera = true;
    [FormerlySerializedAs("wEffectSpinSpeed")]
    public float wEffectSelfSpinSpeed = 0f;
    [Header("W SwordEnergy Bonuses")]
    public float wDurationPerSwordEnergy = 0f;
    public float wMaxDurationBonus = 0f;
    public float wOrbitSpeedPerSwordEnergy = 0f;
    public float wMaxOrbitSpeedBonus = 0f;
    public float wRadiusPerSwordEnergy = 0f;
    public float wMaxRadiusBonus = 0f;
    [Header("W 剑群漩涡")]
    public float wOrbitRadiusMin = 0.9f;
    public float wOrbitRadiusMax = 1.8f;
    public float wHeightMin = 0.2f;
    public float wHeightMax = 1.2f;
    public float wOrbitSpeedMin = 60f;
    public float wOrbitSpeedMax = 120f;
    public float wBobAmplitudeMin = 0.05f;
    public float wBobAmplitudeMax = 0.25f;
    public float wBobFrequencyMin = 0.8f;
    public float wBobFrequencyMax = 2.0f;
    public float wSwingAngleMin = 3f;
    public float wSwingAngleMax = 12f;
    public float wRadiusJitter = 0.12f;
    public float wAngularJitter = 10f;
    public bool wClockwise = true;
    [Header("W 剑群朝向")]
    public bool wFaceOrbitDirection = true;
    public float wOrbitDirectionYawOffset = 0f;
    public float wOrbitDirectionPitchOffset = 0f;
    public float wOrbitDirectionRollOffset = 0f;
    public bool wKeepSwordVisibleToCamera = true;

    [Header("Skill Effect Visuals - E")]
    public Vector3 eEffectScale = new Vector3(0.35f, 0.35f, 0.35f);
    public float eEffectRotationZ = -90f;
    public Vector3 eEffectOffset = Vector3.zero;
    public Vector3 eEffectPlaneScale = new Vector3(0.35f, 0.35f, 1f);
    public float eEffectYawOffset = 0f;
    public float eEffectVisualPitch = 0f;
    public float eEffectVisualYaw = 0f;
    public float eEffectVisualRoll = 0f;

    [Header("Skill Effect Visuals - R")]
    public Vector3 rEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    public float rEffectRotationZ = -90f;
    public Vector3 rEffectOffset = Vector3.zero;
    public Vector3 rEffectPlaneScale = new Vector3(0.3f, 0.3f, 1f);
    public float rEffectYawOffset = 0f;
    public float rEffectVisualPitch = 0f;
    public float rEffectVisualYaw = 0f;
    public float rEffectVisualRoll = 0f;
    public bool rEffectInvertForward = false;
    [Header("R 万剑漩涡")]
    public float rSwarmDuration = 2.0f;
    public float rSwarmRadiusMin = 0.8f;
    public float rSwarmRadiusMax = 3.2f;
    public float rSwarmHeightMin = 0.4f;
    public float rSwarmHeightMax = 3.0f;
    public float rSwarmSpeedMin = 120f;
    public float rSwarmSpeedMax = 300f;
    public float rSwarmBobAmplitudeMin = 0.05f;
    public float rSwarmBobAmplitudeMax = 0.35f;
    public float rSwarmBobFrequencyMin = 0.8f;
    public float rSwarmBobFrequencyMax = 2.5f;
    public float rSwarmRadiusJitter = 0.25f;
    public bool rSwarmClockwise = true;
    public float rSwarmForwardOffset = 2.0f;
    public float rSwarmYawOffset = 0f;
    [Header("R 剑自身旋转")]
    public bool rEnableSwordSelfSpin = true;
    public float rSwordSelfSpinMin = 30f;
    public float rSwordSelfSpinMax = 120f;
    public Vector3 rSwordLengthLocalAxis = Vector3.up;
    [Header("R 万剑漩涡伤害")]
    public float rSwarmDamageRadius = 3.0f;
    public float rSwarmDamageInterval = 0.25f;
    public float rSwarmDamagePerTick = 2.0f;
    public LayerMask rSwarmEnemyLayer = ~0;

    [Header("Skill Effect Visuals - Standby Sword")]
    public Vector3 standbySwordScale = new Vector3(0.25f, 0.25f, 0.25f);
    public float standbySwordRotationZ = -90f;
    public Vector3 standbySwordPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    public Vector3 standbySwordOffset = Vector3.zero;
    public float standbySwordVisualPitch = 90f;
    public float standbySwordVisualYaw = 0f;
    public float standbySwordVisualRoll = 0f;
    public float standbySwordSpinSpeed = 120f;

    [Header("Refs")]
    public Rigidbody rb;

    private sealed class SkillEffectRuntime : MonoBehaviour
    {
        public Transform visual;
        public Vector3 baseVisualScale;
        public Material[] materialTargets;
        public Color[] materialBaseColors;
        public SpriteRenderer[] spriteTargets;
        public Color[] spriteBaseColors;
    }

    private sealed class RSwarmSwordData
    {
        public GameObject sword;
        public float baseAngle;
        public float radius;
        public float height;
        public float orbitSpeed;
        public float bobAmplitude;
        public float bobFrequency;
        public float phase;
        public float layerOffset;
        public SkillEffectRuntime runtime;
        public Transform visualTransform;
        public Quaternion baseVisibleLocalRotation;
        public float selfSpinSpeed;
        public float selfSpinAngle;
    }

    private Vector3 lastMoveDir = Vector3.forward;
    private int standbySwords;
    private bool isDashing;
    private bool isShielding;
    private bool isWGuardActive;
    private float wOrbitAngle;
    private Coroutine wSkillRoutine;
    private Coroutine rSwarmRoutine;
    private GameObject activeWOrbitVisualRoot;
    private readonly List<GameObject> activeWSwords = new List<GameObject>();
    private GameObject activeRSwarmRoot;
    private readonly List<RSwarmSwordData> activeRSwarmSwords = new List<RSwarmSwordData>();
    private int currentWSwordCount;
    private float currentWFinalDamageReduction;

    private readonly List<GameObject> standbySwordVisuals = new List<GameObject>();

    private Quaternion initialRotation;

    private void Awake()
    {
        initialRotation = transform.rotation;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private void LateUpdate()
    {
        if (lockCharacterRotation)
        {
            transform.rotation = initialRotation;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame) CastQ();
        if (Keyboard.current.wKey.wasPressedThisFrame) CastW();
        if (Keyboard.current.eKey.wasPressedThisFrame) CastE();
        if (Keyboard.current.rKey.wasPressedThisFrame) CastR();
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        Vector2 input = ReadMoveInput();
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            lastMoveDir = moveDir.normalized;
        }

        Vector3 delta = new Vector3(moveDir.x, 0f, moveDir.z) * moveSpeed * Time.fixedDeltaTime;
        transform.position += delta;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
        if (Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        if (Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
        if (Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        return Vector2.ClampMagnitude(input, 1f);
    }

    private void CastQ()
    {
        Vector3 dir = ResolveFacingDirection();
        Vector3 spawnPos = transform.position + Vector3.up * 1.2f + transform.right * 0.8f + qEffectOffset;
        GameObject sword = CreateSkillEffectVisual(
            "Q_Sword",
            qSkillEffectPrefab,
            spawnPos,
            dir,
            true,
            qEffectInvertForward,
            qEffectYawOffset,
            qEffectVisualPitch,
            qEffectVisualYaw,
            qEffectVisualRoll + ResolveRotation(qEffectRotationZ),
            ResolveVisualScale(qEffectScale, qEffectPlaneScale));
        StartCoroutine(FireAfterDelay(sword, dir, qDelay, qSwordSpeed));
        currentSwordEnergy += 1;
    }

    private void CastW()
    {
        if (wSkillRoutine != null)
        {
            StopCoroutine(wSkillRoutine);
            wSkillRoutine = null;
        }

        CleanupWVisuals();
        wSkillRoutine = StartCoroutine(ShieldRoutine());
    }

    private void CastE()
    {
        if (!isDashing) StartCoroutine(DashRoutine());
        Vector3 eDirection = ResolveFacingDirection();
        LaunchStandbySwords(eDirection, 18f);
    }

    private void CastR()
    {
        if (rSwarmRoutine != null)
        {
            StopCoroutine(rSwarmRoutine);
            rSwarmRoutine = null;
        }
        CleanupRSwarmVisuals();

        int energyForR = Mathf.Max(0, currentSwordEnergy);
        int count = Mathf.Max(0, rBaseSwordCount) + energyForR;
        if (count <= 0) return;
        Debug.Log($"[R Skill] BaseSwordCount={rBaseSwordCount}, CurrentSwordEnergy={energyForR}, Spawned={count}", this);
        currentSwordEnergy = 0;
        rSwarmRoutine = StartCoroutine(RSwarmRoutine(count));
    }

    private IEnumerator RSwarmRoutine(int count)
    {
        Vector3 attackDir = ResolveFacingDirection();
        Vector3 center = transform.position + attackDir.normalized * rSwarmForwardOffset + rEffectOffset;

        GameObject swarmRoot = new GameObject("R_SwarmVisualRoot");
        swarmRoot.transform.position = center;
        swarmRoot.transform.rotation = Quaternion.identity;
        activeRSwarmRoot = swarmRoot;
        activeRSwarmSwords.Clear();

        for (int i = 0; i < count; i++)
        {
            float baseAngle = i * (360f / Mathf.Max(1, count)) + Random.Range(-30f, 30f);
            float radius = Random.Range(Mathf.Min(rSwarmRadiusMin, rSwarmRadiusMax), Mathf.Max(rSwarmRadiusMin, rSwarmRadiusMax));
            float height = Random.Range(Mathf.Min(rSwarmHeightMin, rSwarmHeightMax), Mathf.Max(rSwarmHeightMin, rSwarmHeightMax));
            float orbitSpeed = Random.Range(Mathf.Min(rSwarmSpeedMin, rSwarmSpeedMax), Mathf.Max(rSwarmSpeedMin, rSwarmSpeedMax));
            float bobAmplitude = Random.Range(Mathf.Min(rSwarmBobAmplitudeMin, rSwarmBobAmplitudeMax), Mathf.Max(rSwarmBobAmplitudeMin, rSwarmBobAmplitudeMax));
            float bobFrequency = Random.Range(Mathf.Min(rSwarmBobFrequencyMin, rSwarmBobFrequencyMax), Mathf.Max(rSwarmBobFrequencyMin, rSwarmBobFrequencyMax));
            float phase = Random.Range(0f, Mathf.PI * 2f);

            float rad = baseAngle * Mathf.Deg2Rad;
            Vector3 spawnOffset = new Vector3(
                Mathf.Cos(rad) * radius,
                height,
                Mathf.Sin(rad) * radius);

            GameObject sword = CreateSkillEffectVisual(
                $"R_SwarmSword_{i}",
                rSkillEffectPrefab,
                center + spawnOffset,
                spawnOffset,
                false,
                false,
                0f,
                rEffectVisualPitch,
                rEffectVisualYaw,
                rEffectVisualRoll + ResolveRotation(rEffectRotationZ),
                ResolveVisualScale(rEffectScale, rEffectPlaneScale));

            if (sword == null)
            {
                continue;
            }

            sword.transform.SetParent(swarmRoot.transform, true);
            SkillEffectRuntime runtime = sword.GetComponent<SkillEffectRuntime>();
            Transform visualTransform = runtime != null && runtime.visual != null ? runtime.visual : null;
            Quaternion baseVisualLocalRotation = visualTransform != null ? visualTransform.localRotation : Quaternion.identity;
            float selfSpinSpeed = Random.Range(Mathf.Min(rSwordSelfSpinMin, rSwordSelfSpinMax), Mathf.Max(rSwordSelfSpinMin, rSwordSelfSpinMax));
            activeRSwarmSwords.Add(new RSwarmSwordData
            {
                sword = sword,
                baseAngle = baseAngle,
                radius = radius,
                height = height,
                orbitSpeed = orbitSpeed,
                bobAmplitude = bobAmplitude,
                bobFrequency = bobFrequency,
                phase = phase,
                layerOffset = Random.Range(-0.25f, 0.25f),
                runtime = runtime,
                visualTransform = visualTransform,
                baseVisibleLocalRotation = baseVisualLocalRotation,
                selfSpinSpeed = selfSpinSpeed,
                selfSpinAngle = Random.Range(0f, 360f)
            });
        }

        float elapsed = 0f;
        float damageTickTimer = 0f;
        float safeDamageInterval = Mathf.Max(0.05f, rSwarmDamageInterval);
        while (elapsed < rSwarmDuration)
        {
            center = transform.position + attackDir.normalized * rSwarmForwardOffset + rEffectOffset;
            swarmRoot.transform.position = center;

            float dirSign = rSwarmClockwise ? -1f : 1f;
            for (int i = 0; i < activeRSwarmSwords.Count; i++)
            {
                RSwarmSwordData data = activeRSwarmSwords[i];
                if (data == null || data.sword == null)
                {
                    continue;
                }

                float angle = data.baseAngle + dirSign * data.orbitSpeed * elapsed;
                float rad = angle * Mathf.Deg2Rad;
                float dynamicRadius = data.radius + Mathf.Sin(elapsed * 1.7f + data.phase) * rSwarmRadiusJitter;
                float dynamicHeight = data.height + data.layerOffset + Mathf.Sin(elapsed * data.bobFrequency + data.phase) * data.bobAmplitude;
                Vector3 offset = new Vector3(
                    Mathf.Cos(rad) * dynamicRadius,
                    dynamicHeight,
                    Mathf.Sin(rad) * dynamicRadius);

                data.sword.transform.position = center + offset;

                Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                if (rSwarmClockwise)
                {
                    tangent = -tangent;
                }

                if (tangent.sqrMagnitude > 0.0001f)
                {
                    Quaternion orbitFacing = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(0f, rSwarmYawOffset, 0f);
                    data.sword.transform.rotation = orbitFacing;
                }

                if (data.visualTransform != null)
                {
                    Quaternion selfSpin = Quaternion.identity;
                    if (rEnableSwordSelfSpin)
                    {
                        data.selfSpinAngle += data.selfSpinSpeed * Time.deltaTime;
                        Vector3 spinAxis = rSwordLengthLocalAxis.sqrMagnitude > 0.0001f ? rSwordLengthLocalAxis.normalized : Vector3.up;
                        selfSpin = Quaternion.AngleAxis(data.selfSpinAngle, spinAxis);
                    }

                    data.visualTransform.localRotation = data.baseVisibleLocalRotation * selfSpin;
                }
            }

            damageTickTimer -= Time.deltaTime;
            if (damageTickTimer <= 0f)
            {
                ApplyRSwarmTickDamage(center);
                damageTickTimer += safeDamageInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        CleanupRSwarmVisuals();
        rSwarmRoutine = null;
    }

    private IEnumerator FireAfterDelay(GameObject effectRoot, Vector3 dir, float delay, float speed)
    {
        float t = 0f;
        while (t < delay)
        {
            if (effectRoot == null) yield break;
            t += Time.deltaTime;
            yield return null;
        }

        float life = 2.2f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            if (effectRoot == null) yield break;
            effectRoot.transform.position += dir.normalized * speed * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (effectRoot != null) Destroy(effectRoot);
    }

    private IEnumerator ShieldRoutine()
    {
        isShielding = true;
        GameObject orbitRoot = new GameObject("W_OrbitVisualRoot");
        orbitRoot.transform.position = transform.position;
        orbitRoot.transform.rotation = Quaternion.identity;
        activeWOrbitVisualRoot = orbitRoot;

        int energyForW = useSwordEnergyForW ? Mathf.Max(0, currentSwordEnergy) : 0;
        int swordCount = baseWSwordCount;
        if (useSwordEnergyForW)
        {
            swordCount += energyForW;
        }
        swordCount = Mathf.Clamp(swordCount, baseWSwordCount, maxWSwordCount);
        wSwordCount = swordCount;

        float finalDuration = wDuration + Mathf.Min(energyForW * wDurationPerSwordEnergy, wMaxDurationBonus);
        float finalOrbitSpeed = wEffectOrbitSpeed + Mathf.Min(energyForW * wOrbitSpeedPerSwordEnergy, wMaxOrbitSpeedBonus);
        float finalRadius = wEffectOrbitRadius + Mathf.Min(energyForW * wRadiusPerSwordEnergy, wMaxRadiusBonus);

        activeWSwords.Clear();
        for (int i = 0; i < swordCount; i++)
        {
            float angle = i * (360f / swordCount);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * finalRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset.x, wEffectHeight, offset.z) + wEffectOffset;

            GameObject sword = CreateSkillEffectVisual(
                $"W_Sword_{i}",
                ResolveWVisualPrefab(),
                spawnPos,
                offset,
                false,
                false,
                0f,
                standbySwordVisualPitch,
                standbySwordVisualYaw,
                standbySwordVisualRoll + ResolveRotation(standbySwordRotationZ),
                ResolveVisualScale(standbySwordScale, standbySwordPlaneScale));

            if (sword == null)
            {
                continue;
            }

            Quaternion extraRot = Quaternion.Euler(
                wEffectVisualPitch,
                wEffectVisualYaw,
                wEffectVisualRoll);
            sword.transform.rotation = sword.transform.rotation * extraRot;

            sword.transform.SetParent(orbitRoot.transform, true);

            // W size control: only use W Effect Scale Multiplier on top of the correctly displayed base scale.
            Vector3 baseScale = sword.transform.localScale;
            float sizeMul = Mathf.Max(0.01f, wEffectScaleMultiplier);
            Vector3 finalScale = baseScale * sizeMul;
            sword.transform.localScale = finalScale;
            Debug.Log($"[W Skill Scale] sword={sword.name}, baseScale={baseScale}, multiplier={sizeMul:F2}, finalScale={finalScale}", this);

            activeWSwords.Add(sword);
        }

        currentWSwordCount = activeWSwords.Count;
        currentWFinalDamageReduction = ComputeWFinalDamageReduction(currentWSwordCount);
        isWGuardActive = true;

        Debug.Log($"[W Skill] Base={baseWSwordCount}, CurrentSwordEnergy={energyForW}, Spawned={activeWSwords.Count}, Duration={finalDuration:F2}, OrbitSpeed={finalOrbitSpeed:F2}, Radius={finalRadius:F2}, DamageReduction={currentWFinalDamageReduction:F2}", this);
        if (activeWSwords.Count > swordCount)
        {
            Debug.LogWarning($"[W Skill] Spawned sword count exceeded expected {swordCount}: {activeWSwords.Count}", this);
        }

        if (activeWSwords.Count == 0)
        {
            CleanupWVisuals();
            isShielding = false;
            yield break;
        }

        wOrbitAngle = 0f;
        float t = 0f;
        while (t < finalDuration)
        {
            orbitRoot.transform.position = transform.position;
            orbitRoot.transform.rotation = Quaternion.identity;

            wOrbitAngle += finalOrbitSpeed * Time.deltaTime;
            for (int i = 0; i < activeWSwords.Count; i++)
            {
                GameObject sword = activeWSwords[i];
                if (sword == null)
                {
                    continue;
                }

                float baseAngle = wOrbitAngle + i * (360f / swordCount);
                float rad = baseAngle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(rad) * finalRadius,
                    wEffectHeight,
                    Mathf.Sin(rad) * finalRadius);

                sword.transform.position = transform.position + offset + wEffectOffset;
            }

            t += Time.deltaTime;
            yield return null;
        }

        CleanupWVisuals();
        isShielding = false;
        wSkillRoutine = null;
    }

    private void CleanupWVisuals()
    {
        for (int i = 0; i < activeWSwords.Count; i++)
        {
            GameObject sword = activeWSwords[i];
            if (sword != null)
            {
                Destroy(sword);
            }
        }
        activeWSwords.Clear();

        if (activeWOrbitVisualRoot != null)
        {
            Destroy(activeWOrbitVisualRoot);
            activeWOrbitVisualRoot = null;
        }

        // Clean legacy/temporary W visuals that may have been spawned by older logic.
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform tr = allTransforms[i];
            if (tr == null) continue;
            string n = tr.name;
            bool isLegacyW =
                n == "W_OrbitRoot" ||
                n == "W_OrbitVisualRoot" ||
                n == "W_Sword" ||
                n == "W_SwordInstance" ||
                n.StartsWith("W_Sword_") ||
                n.StartsWith("W_SwordPivot_");

            if (isLegacyW)
            {
                Destroy(tr.gameObject);
            }
        }

        standbySwords = 0;
        isShielding = false;
        isWGuardActive = false;
        currentWSwordCount = 0;
        currentWFinalDamageReduction = 0f;
    }

    private void CleanupRSwarmVisuals()
    {
        for (int i = 0; i < activeRSwarmSwords.Count; i++)
        {
            RSwarmSwordData data = activeRSwarmSwords[i];
            if (data != null && data.sword != null)
            {
                Destroy(data.sword);
            }
        }
        activeRSwarmSwords.Clear();

        if (activeRSwarmRoot != null)
        {
            Destroy(activeRSwarmRoot);
            activeRSwarmRoot = null;
        }
    }

    private void ApplyRSwarmTickDamage(Vector3 center)
    {
        if (rSwarmDamagePerTick <= 0f || rSwarmDamageRadius <= 0f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(center, rSwarmDamageRadius, rSwarmEnemyLayer);
        HashSet<GameObject> damagedRoots = new HashSet<GameObject>();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Transform targetRoot = hit.transform.root;
            if (targetRoot == null || targetRoot.gameObject == gameObject || !damagedRoots.Add(targetRoot.gameObject))
            {
                continue;
            }

            CombatHealth combatHealth = targetRoot.GetComponentInParent<CombatHealth>();
            if (combatHealth != null && combatHealth.gameObject != gameObject)
            {
                combatHealth.TakeDamage(new BattleDamage(rSwarmDamagePerTick, BattleDamageType.Physical, gameObject));
                continue;
            }

            EnemyHealth enemyHealth = targetRoot.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.gameObject != gameObject)
            {
                int damageInt = Mathf.Max(1, Mathf.RoundToInt(rSwarmDamagePerTick));
                enemyHealth.TakeDamage(damageInt, gameObject);
            }
        }
    }

    private GameObject ResolveWVisualPrefab()
    {
        if (standbySkillEffectPrefab != null)
        {
            return standbySkillEffectPrefab;
        }

        if (wSkillEffectPrefab != null)
        {
            return wSkillEffectPrefab;
        }

        return sharedSkillEffectPrefab;
    }

    private float ComputeWFinalDamageReduction(int wSwordCountAtCast)
    {
        float reduction = wDamageReduction + Mathf.Max(0, wSwordCountAtCast) * wDamageReductionPerSword;
        return Mathf.Clamp(reduction, 0f, wMaxDamageReduction);
    }

    public float ProcessIncomingDamageWithWGuard(float rawDamage, BattleDamage incomingDamage)
    {
        float clampedRaw = Mathf.Max(0f, rawDamage);
        if (!isWGuardActive)
        {
            return clampedRaw;
        }

        float blockedDamage = clampedRaw * currentWFinalDamageReduction;
        float damageAfterReduction = clampedRaw - blockedDamage;
        float counterDamage = blockedDamage * wCounterDamageRatio;

        Debug.Log($"[W Guard] Raw={clampedRaw:F2}, Blocked={blockedDamage:F2}, Taken={damageAfterReduction:F2}, Counter={counterDamage:F2}", this);
        ApplyWCounterDamage(incomingDamage, counterDamage);
        return Mathf.Max(0f, damageAfterReduction);
    }

    private void ApplyWCounterDamage(BattleDamage incomingDamage, float counterDamage)
    {
        if (counterDamage <= 0f)
        {
            return;
        }

        GameObject attacker = incomingDamage.source;
        if (attacker == null)
        {
            Debug.LogWarning("[W Guard] Counter requires attacker/source reference in BattleDamage.", this);
            return;
        }

        if (attacker == gameObject)
        {
            return;
        }

        CombatHealth attackerCombatHealth = attacker.GetComponentInParent<CombatHealth>();
        if (attackerCombatHealth != null && attackerCombatHealth.gameObject != gameObject)
        {
            attackerCombatHealth.TakeDamage(new BattleDamage(counterDamage, incomingDamage.damageType, gameObject));
            return;
        }

        EnemyHealth attackerEnemyHealth = attacker.GetComponentInParent<EnemyHealth>();
        if (attackerEnemyHealth != null && attackerEnemyHealth.gameObject != gameObject)
        {
            int roundedDamage = Mathf.Max(1, Mathf.RoundToInt(counterDamage));
            attackerEnemyHealth.TakeDamage(roundedDamage, gameObject);
            return;
        }

        Debug.LogWarning($"[W Guard] Attacker '{attacker.name}' has no CombatHealth/EnemyHealth for counter damage.", this);
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        Vector3 dir = ResolveFacingDirection();
        Vector3 start = transform.position;
        Vector3 end = start + dir * dashDistance;

        float t = 0f;
        while (t < dashDuration)
        {
            float p = Mathf.Clamp01(t / dashDuration);
            transform.position = Vector3.Lerp(start, end, p);

            if (Random.value < 0.45f)
            {
                Vector3 trailPos = transform.position + Vector3.up * 0.5f + eEffectOffset;
                GameObject trail = CreateSkillEffectVisual(
                    "E_Rail",
                    eSkillEffectPrefab,
                    trailPos,
                    dir,
                    true,
                    false,
                    eEffectYawOffset,
                    eEffectVisualPitch,
                    eEffectVisualYaw,
                    eEffectVisualRoll + ResolveRotation(eEffectRotationZ),
                    ResolveVisualScale(eEffectScale, eEffectPlaneScale));
                StartCoroutine(FadeAndDestroy(trail, eRailDuration));
            }

            t += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
        isDashing = false;
    }

    private IEnumerator FadeAndDestroy(GameObject effectRoot, float duration)
    {
        if (effectRoot == null) yield break;
        SkillEffectRuntime runtime = effectRoot.GetComponent<SkillEffectRuntime>();
        float t = 0f;
        while (t < duration)
        {
            if (effectRoot == null) yield break;
            float alpha = 1f - (t / duration);
            ApplyFadeAlpha(runtime, alpha);
            t += Time.deltaTime;
            yield return null;
        }

        if (effectRoot != null) Destroy(effectRoot);
    }

    private void AddStandbySword()
    {
        if (standbySwords >= maxStandbySwords) return;

        standbySwords += 1;
        Vector3 orbitOffset = Quaternion.Euler(0f, standbySwords * 360f / maxStandbySwords, 0f) * Vector3.forward * 1.1f;
        GameObject standby = CreateSkillEffectVisual(
            "StandbySword",
            standbySkillEffectPrefab,
            transform.position + Vector3.up + orbitOffset + standbySwordOffset,
            orbitOffset,
            false,
            false,
            0f,
            standbySwordVisualPitch,
            standbySwordVisualYaw,
            standbySwordVisualRoll + ResolveRotation(standbySwordRotationZ),
            ResolveVisualScale(standbySwordScale, standbySwordPlaneScale));
        standbySwordVisuals.Add(standby);
        StartCoroutine(OrbitStandbySword(standby, standbySwords - 1));
    }

    private IEnumerator OrbitStandbySword(GameObject standby, int index)
    {
        SkillEffectRuntime runtime = standby != null ? standby.GetComponent<SkillEffectRuntime>() : null;
        while (standby != null && standbySwords > 0)
        {
            float angle = Time.time * 120f + index * 120f;
            Vector3 orbitOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.1f;
            standby.transform.position = transform.position + Vector3.up + orbitOffset + standbySwordOffset;

            if (runtime != null && runtime.visual != null && standbySwordSpinSpeed != 0f)
            {
                runtime.visual.Rotate(Vector3.up, standbySwordSpinSpeed * Time.deltaTime, Space.Self);
            }

            yield return null;
        }
    }

    private void LaunchStandbySwords(Vector3 dir, float speed)
    {
        foreach (GameObject standby in standbySwordVisuals)
        {
            if (standby == null) continue;
            if (!useRawPrefabRotationForSkillEffects)
            {
                ApplyRootDirection(standby.transform, dir, true, false, 0f);
            }
            StartCoroutine(FireAfterDelay(standby, dir, 0f, speed));
        }

        standbySwordVisuals.Clear();
        standbySwords = 0;
    }

    private Vector3 ResolveFacingDirection()
    {
        if (lastMoveDir.sqrMagnitude > 0.0001f) return lastMoveDir.normalized;
        return Vector3.forward;
    }

    private GameObject CreateSkillEffectVisual(
        string name,
        GameObject specificPrefab,
        Vector3 worldPosition,
        Vector3 direction,
        bool alignToDirection,
        bool invertForward,
        float yawOffset,
        float visualPitch,
        float visualYaw,
        float visualRoll,
        Vector3 visualScale)
    {
        GameObject root = new GameObject(name);
        root.transform.position = worldPosition;
        if (useRawPrefabRotationForSkillEffects)
        {
            root.transform.rotation = Quaternion.Euler(skillEffectPrefabBaseRotation);
        }
        else
        {
            ApplyRootDirection(root.transform, direction, alignToDirection, invertForward, yawOffset);
        }

        GameObject effectVisual = CreateEffectInstance(name, specificPrefab, root.transform.position, root.transform.rotation, useRawPrefabRotationForSkillEffects);
        if (effectVisual == null)
        {
            Destroy(root);
            return null;
        }

        // Keep world transform when parenting so instantiated prefab stays at the root skill position.
        effectVisual.transform.SetParent(root.transform, true);

        Transform visualTarget = FindEffectVisualTransform(effectVisual);
        if (useRawPrefabRotationForSkillEffects)
        {
            effectVisual.transform.rotation = Quaternion.Euler(skillEffectPrefabBaseRotation);
            float rawScaleMultiplier = Mathf.Max(0.01f, skillEffectPrefabScaleMultiplier);
            effectVisual.transform.localScale = effectVisual.transform.localScale * rawScaleMultiplier;
        }
        else
        {
            visualTarget.localRotation = Quaternion.Euler(visualPitch, visualYaw, visualRoll);
            visualTarget.localScale = Vector3.Scale(visualTarget.localScale, ClampVisualScale(visualScale));
        }
        EnsureEffectVisible(effectVisual);

        SkillEffectRuntime runtime = root.AddComponent<SkillEffectRuntime>();
        runtime.visual = visualTarget;
        runtime.baseVisualScale = visualTarget.localScale;
        CacheFadeTargets(effectVisual, runtime);

        return root;
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

    private static void EnsureEffectVisible(GameObject effectRoot)
    {
        if (effectRoot == null)
        {
            return;
        }

        effectRoot.SetActive(true);
        Renderer[] renderers = effectRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }
    }

    private static void TrySetDoubleSidedIfSupported(GameObject effectRoot)
    {
        if (effectRoot == null)
        {
            return;
        }

        Renderer[] renderers = effectRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null || !mat.HasProperty("_Cull"))
                {
                    continue;
                }

                mat.SetFloat("_Cull", 0f);
            }
        }
    }

    private GameObject CreateEffectInstance(string effectName, GameObject specificPrefab, Vector3 position, Quaternion rotation, bool preservePrefabRotation)
    {
        GameObject sourcePrefab = specificPrefab != null ? specificPrefab : sharedSkillEffectPrefab;
        if (sourcePrefab != null)
        {
            GameObject instance;
            if (preservePrefabRotation)
            {
                instance = Instantiate(sourcePrefab);
                instance.transform.position = position;
            }
            else
            {
                instance = Instantiate(sourcePrefab, position, rotation);
            }

            return instance;
        }

        Debug.LogWarning($"[Player2PrototypeController] Missing skill effect prefab for '{effectName}' on {name}. Assign specific prefab or Shared Skill Effect Prefab.", this);
        return null;
    }

    private static Transform FindEffectVisualTransform(GameObject root)
    {
        MeshRenderer rootMesh = root.GetComponent<MeshRenderer>();
        if (rootMesh != null) return root.transform;

        MeshRenderer childMesh = root.GetComponentInChildren<MeshRenderer>(true);
        if (childMesh != null) return childMesh.transform;

        SpriteRenderer rootSprite = root.GetComponent<SpriteRenderer>();
        if (rootSprite != null) return root.transform;

        SpriteRenderer childSprite = root.GetComponentInChildren<SpriteRenderer>(true);
        if (childSprite != null) return childSprite.transform;

        return root.transform;
    }

    private void CacheFadeTargets(GameObject effectVisualRoot, SkillEffectRuntime runtime)
    {
        List<Material> mats = new List<Material>();
        List<Color> matColors = new List<Color>();

        Renderer[] renderers = effectVisualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] instanceMats = renderers[i].materials;
            for (int m = 0; m < instanceMats.Length; m++)
            {
                Material mat = instanceMats[m];
                if (mat == null) continue;
                mats.Add(mat);
                matColors.Add(GetMaterialColor(mat));
            }
        }

        runtime.materialTargets = mats.ToArray();
        runtime.materialBaseColors = matColors.ToArray();

        runtime.spriteTargets = effectVisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        runtime.spriteBaseColors = new Color[runtime.spriteTargets.Length];
        for (int i = 0; i < runtime.spriteTargets.Length; i++)
        {
            runtime.spriteBaseColors[i] = runtime.spriteTargets[i].color;
        }
    }

    private static void ApplyFadeAlpha(SkillEffectRuntime runtime, float alpha)
    {
        if (runtime == null)
        {
            return;
        }

        if (runtime.materialTargets != null)
        {
            for (int i = 0; i < runtime.materialTargets.Length; i++)
            {
                Material mat = runtime.materialTargets[i];
                if (mat == null) continue;
                Color baseColor = i < runtime.materialBaseColors.Length ? runtime.materialBaseColors[i] : Color.white;
                SetMaterialColor(mat, new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha));
            }
        }

        if (runtime.spriteTargets != null)
        {
            for (int i = 0; i < runtime.spriteTargets.Length; i++)
            {
                SpriteRenderer sr = runtime.spriteTargets[i];
                if (sr == null) continue;
                Color baseColor = i < runtime.spriteBaseColors.Length ? runtime.spriteBaseColors[i] : Color.white;
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
            }
        }
    }

    private static void ApplyRootDirection(Transform root, Vector3 direction, bool alignToDirection, bool invertForward, float yawOffset)
    {
        float yaw = 0f;
        if (alignToDirection && direction.sqrMagnitude > 0.0001f)
        {
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        if (invertForward)
        {
            yaw += 180f;
        }

        root.rotation = Quaternion.Euler(0f, yaw + yawOffset, 0f);
    }

    private Vector3 ResolveVisualScale(Vector3 specificScale, Vector3 planeScale)
    {
        Vector3 baseScale = sharedEffectScale;
        Vector3 roleScale = specificScale.sqrMagnitude > 0.0001f ? specificScale : Vector3.one;
        Vector3 quadScale = planeScale.sqrMagnitude > 0.0001f ? planeScale : Vector3.one;
        return new Vector3(
            baseScale.x * roleScale.x * quadScale.x,
            baseScale.y * roleScale.y * quadScale.y,
            baseScale.z * roleScale.z * quadScale.z);
    }

    private float ResolveRotation(float specificRotationZ)
    {
        return sharedEffectRotationZ + specificRotationZ;
    }

    private static Color GetMaterialColor(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return Color.white;
    }

    private static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }
}
