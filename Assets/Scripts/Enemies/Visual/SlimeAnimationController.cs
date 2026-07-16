using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class SlimeAnimationController : MonoBehaviour
{
    [Serializable]
    public class AttackHitEvent : UnityEvent<Transform> { }

    [Header("Visual Binding")]
    [SerializeField] private Transform visualRoot;
    [FormerlySerializedAs("visualSpriteRenderer")]
    [SerializeField] private SpriteRenderer slimeSpriteRenderer;
    [SerializeField] private bool autoBindVisualByName = true;

    [Header("Facing")]
    [SerializeField] private bool enableAutoFlip = true;
    [SerializeField] private bool spriteDefaultFacesLeft = true;
    [SerializeField, Min(0f)] private float facingDeadZone = 0.05f;
    [SerializeField] private bool debugFacing = false;

    [Header("Move - Slime Move Wobble")]
    [SerializeField] private float moveWobbleSpeed = 9f;
    [SerializeField] private float moveSquashAmount = 0.12f;
    [SerializeField] private float moveStretchAmount = 0.15f;
    [SerializeField] private float moveTiltAmount = 10f;
    [SerializeField] private float moveBobAmount = 0.03f;
    [SerializeField] private float moveSpeedForMaxWobble = 4f;

    [Header("Attack - Slime Jump Attack")]
    [SerializeField] private float attackChargeTime = 0.18f;
    [SerializeField] private float attackJumpTime = 0.24f;
    [SerializeField] private float attackRecoverTime = 0.2f;
    [SerializeField] private float attackJumpHeight = 0.8f;
    [SerializeField] private float attackSquashAmount = 0.2f;
    [SerializeField] private float attackStretchAmount = 0.22f;
    [SerializeField] private float attackStopDistance = 0.7f;
    [SerializeField] private bool allowAttackForwardLeap = true;
    [SerializeField] private bool allowAttackVerticalLeap = true;
    [SerializeField] private float maxAttackLeapDistance = 0.65f;

    [Header("Attack Landing VFX")]
    [SerializeField] private bool enableAttackLandingVfx = true;
    [SerializeField] private GameObject attackLandingVfxPrefab;
    [SerializeField] private Vector3 attackLandingVfxOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float attackLandingVfxLifetime = 2f;

    [Header("Visibility")]
    [SerializeField] private float minimumVisibleAlpha = 0.92f;

    [Header("Death - Slime Death Dissolve")]
    [SerializeField] private float deathDuration = 0.65f;
    [SerializeField] private float deathFadeTime = 0.45f;
    [SerializeField] private int deathParticleCount = 14;
    [SerializeField] private float deathParticleSpeed = 1.1f;
    [SerializeField] private Color deathParticleColor = new Color(0.48f, 0.95f, 0.9f, 1f);
    [SerializeField] private bool destroyAfterDeath = true;

    [Header("Health Hook")]
    [SerializeField] private bool autoPlayDeathOnHealthEvent = true;

    public event Action<Transform> OnAttackHit;
    public AttackHitEvent onAttackHit = new AttackHitEvent();
    public bool IsAttacking => isAttacking;
    public bool IsDying => isDying;
    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public bool IsVisualPresentationVisible
    {
        get
        {
            Transform resolvedVisualRoot = VisualRoot;
            if (resolvedVisualRoot == null || !resolvedVisualRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (slimeSpriteRenderer != null)
            {
                return slimeSpriteRenderer.gameObject.activeInHierarchy
                    && slimeSpriteRenderer.enabled
                    && slimeSpriteRenderer.color.a > 0.01f;
            }

            Renderer[] renderers = resolvedVisualRoot.GetComponentsInChildren<Renderer>(true);
            bool hasRenderer = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                hasRenderer = true;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return !hasRenderer;
        }
    }
    public float AttackWindup => Mathf.Max(0.01f, attackChargeTime);
    public float AttackRecovery => Mathf.Max(0.01f, attackRecoverTime);
    public float AttackAnimationDuration => Mathf.Max(0.01f, attackChargeTime) + Mathf.Max(0.01f, attackJumpTime) + Mathf.Max(0.01f, attackRecoverTime);
    public string SelectedAttackClipName => "ProceduralJumpAttack";
    public float SelectedAttackClipLength => AttackAnimationDuration;
    public float AnimatorPlaybackSpeed => 1f;
    public bool AllowAttackForwardLeap => allowAttackForwardLeap;
    public bool AllowAttackVerticalLeap => allowAttackVerticalLeap;
    public float MaxAttackLeapDistance => maxAttackLeapDistance;
    public Vector3 BaseVisualLocalScale => baseVisualLocalScale;
    public Vector3 BaseVisualLocalPosition => baseVisualLocalPosition;

    private CombatHealth combatHealth;
    private Rigidbody ownerRigidbody;
    private EnemyController ownerEnemyController;
    private bool previousCombatDestroyOnDeath = true;
    private bool hookedHealth;

    private Vector3 baseVisualLocalScale;
    private Vector3 baseVisualLocalPosition;
    private Quaternion baseVisualLocalRotation;
    private Color baseVisualColor = Color.white;
    private float spriteHalfHeightLocal = 0.5f;

    private Vector2 currentMoveDirection = Vector2.right;
    private float currentMoveSpeed;
    private bool moveActive;
    private float moveClock;

    private bool isAttacking;
    private bool isDying;
    private bool facingRight;
    private bool hasFacingDirection;
    private Coroutine attackRoutine;
    private Coroutine deathRoutine;

    private ParticleSystem deathParticles;

    private void Awake()
    {
        ResolveVisualReferences();
        CaptureVisualBaseState();
        CacheOwnerReferences();
        EnsureDeathParticles();
        HookHealthEvents();
    }

    private void OnEnable()
    {
        CacheOwnerReferences();
        if (!hookedHealth)
        {
            HookHealthEvents();
        }
    }

    private void OnDisable()
    {
        UnhookHealthEvents();
    }

    private void Update()
    {
        UpdateFacing();

        if (isDying || isAttacking || visualRoot == null)
        {
            return;
        }

        if (moveActive && currentMoveSpeed > 0.001f)
        {
            ApplyMoveWobble(Time.deltaTime);
        }
        else
        {
            RestoreVisualPose(Time.deltaTime * 10f);
        }
    }

    public void PlayMoveAnimation(Vector2 moveDirection, float speed)
    {
        if (isDying)
        {
            return;
        }

        moveActive = true;
        currentMoveSpeed = Mathf.Max(0f, speed);
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            currentMoveDirection = moveDirection.normalized;
        }
    }

    public void StopMoveAnimation()
    {
        moveActive = false;
        currentMoveSpeed = 0f;
    }

    public void SetFacingRight(bool desiredFacingRight, string source = "External")
    {
        if (slimeSpriteRenderer == null)
        {
            ResolveVisualReferences();
        }

        if (slimeSpriteRenderer == null)
        {
            return;
        }

        ApplyFacing(desiredFacingRight, source);
    }

    public void SetVisualBaseScale(Vector3 newBaseScale)
    {
        if (visualRoot == null)
        {
            return;
        }

        baseVisualLocalScale = newBaseScale;
        visualRoot.localScale = newBaseScale;

        if (slimeSpriteRenderer != null && slimeSpriteRenderer.sprite != null)
        {
            spriteHalfHeightLocal = slimeSpriteRenderer.sprite.bounds.size.y * Mathf.Abs(baseVisualLocalScale.y) * 0.5f;
        }
    }

    public void SetVisualBasePosition(Vector3 newBasePosition)
    {
        if (visualRoot == null)
        {
            return;
        }

        baseVisualLocalPosition = newBasePosition;
        visualRoot.localPosition = newBasePosition;
    }

    public void PlayAttack(Transform target)
    {
        if (isDying || isAttacking)
        {
            return;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }

        attackRoutine = StartCoroutine(AttackRoutine(target));
    }

    public void PlayDeath()
    {
        if (isDying)
        {
            return;
        }

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
        }

        deathRoutine = StartCoroutine(DeathRoutine());
    }

    private IEnumerator AttackRoutine(Transform target)
    {
        isAttacking = true;
        float moveSpeedBeforeAttack = currentMoveSpeed;
        bool moveWasActive = moveActive;
        moveActive = false;
        currentMoveSpeed = 0f;

        float chargeTime = Mathf.Max(0.01f, attackChargeTime);
        float jumpTime = Mathf.Max(0.01f, attackJumpTime);
        float recoverTime = Mathf.Max(0.01f, attackRecoverTime);

        for (float t = 0f; t < chargeTime; t += Time.deltaTime)
        {
            float p = EaseOutCubic(t / chargeTime);
            float xScale = 1f + attackSquashAmount * p;
            float yScale = 1f - attackSquashAmount * 0.8f * p;
            ApplyVisualScaleAndGrounding(xScale, yScale, 1f);
            yield return null;
        }

        ApplyVisualScaleAndGrounding(1f + attackSquashAmount, 1f - attackSquashAmount * 0.8f, 1f);

        Vector3 startWorld = transform.position;
        Vector3 fallbackDirection = new Vector3(currentMoveDirection.x, 0f, currentMoveDirection.y);
        if (fallbackDirection.sqrMagnitude < 0.001f)
        {
            fallbackDirection = transform.forward;
        }
        fallbackDirection.y = 0f;
        fallbackDirection.Normalize();

        Vector3 targetWorld = target != null ? target.position : (startWorld + fallbackDirection * 1.5f);
        Vector3 flatToTarget = targetWorld - startWorld;
        flatToTarget.y = 0f;
        Vector3 jumpDirection = flatToTarget.sqrMagnitude > 0.001f ? flatToTarget.normalized : fallbackDirection;
        float jumpDistance = allowAttackForwardLeap
            ? Mathf.Max(0f, flatToTarget.magnitude - Mathf.Max(0f, attackStopDistance))
            : 0f;
        if (maxAttackLeapDistance > 0f)
        {
            jumpDistance = Mathf.Min(jumpDistance, maxAttackLeapDistance);
        }
        Vector3 endWorld = startWorld + jumpDirection * jumpDistance;

        bool hitRaised = false;
        float travelTiltSign = Mathf.Sign(jumpDirection.x);
        if (Mathf.Abs(travelTiltSign) < 0.001f)
        {
            travelTiltSign = 1f;
        }

        for (float t = 0f; t < jumpTime; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / jumpTime);
            Vector3 flatPos = Vector3.Lerp(startWorld, endWorld, p);
            float arc = allowAttackVerticalLeap
                ? 4f * Mathf.Max(0f, attackJumpHeight) * p * (1f - p)
                : 0f;
            transform.position = new Vector3(flatPos.x, startWorld.y + arc, flatPos.z);

            float airStretch = Mathf.Sin(p * Mathf.PI);
            float xScale = 1f - attackStretchAmount * 0.35f * airStretch;
            float yScale = 1f + attackStretchAmount * airStretch;
            float zScale = 1f - attackStretchAmount * 0.25f * airStretch;
            ApplyVisualScaleAndGrounding(xScale, yScale, zScale);
            visualRoot.localRotation = Quaternion.Euler(
                baseVisualLocalRotation.eulerAngles.x,
                baseVisualLocalRotation.eulerAngles.y,
                baseVisualLocalRotation.eulerAngles.z - travelTiltSign * moveTiltAmount * 0.25f);

            if (!hitRaised && p >= 0.82f)
            {
                hitRaised = true;
                RaiseAttackHit(target);
            }

            yield return null;
        }

        transform.position = endWorld;
        PlayAttackLandingVfx(endWorld, jumpDistance);
        if (!hitRaised)
        {
            RaiseAttackHit(target);
        }

        for (float t = 0f; t < recoverTime; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / recoverTime);
            float damp = 1f - p;
            float squash = Mathf.Sin(p * Mathf.PI) * attackSquashAmount * 0.8f * damp;
            float xScale = 1f + squash;
            float yScale = 1f - squash * 0.9f;
            ApplyVisualScaleAndGrounding(xScale, yScale, 1f);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, baseVisualLocalRotation, Time.deltaTime * 16f);
            yield return null;
        }

        RestoreVisualPose(1f);
        currentMoveSpeed = moveSpeedBeforeAttack;
        moveActive = moveWasActive;
        isAttacking = false;
        attackRoutine = null;
    }

    private IEnumerator DeathRoutine()
    {
        isDying = true;
        moveActive = false;
        currentMoveSpeed = 0f;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        float totalDuration = Mathf.Max(0.08f, deathDuration);
        EmitDeathParticles();

        for (float t = 0f; t < totalDuration; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / totalDuration);
            float xScale = Mathf.Lerp(1f, 1.28f, p);
            float yScale = Mathf.Lerp(1f, 0.14f, p);
            float zScale = Mathf.Lerp(1f, 1.08f, p);
            ApplyVisualScaleAndGrounding(xScale, yScale, zScale);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, baseVisualLocalRotation, Time.deltaTime * 18f);
            SetVisualAlpha(Mathf.Lerp(baseVisualColor.a, 0f, p));
            yield return null;
        }

        SetVisualAlpha(0f);
        if (GetComponent<DissolveOnDeath>() != null)
        {
            Debug.Log($"[DeathFlow] Skip immediate destroy because DissolveOnDeath exists owner={name}", this);
            yield break;
        }

        if (destroyAfterDeath)
        {
            Destroy(gameObject);
            yield break;
        }

        gameObject.SetActive(false);
    }

    private void ApplyMoveWobble(float deltaTime)
    {
        float speedFactor = Mathf.Clamp01(currentMoveSpeed / Mathf.Max(0.01f, moveSpeedForMaxWobble));
        float frequency = moveWobbleSpeed * Mathf.Lerp(0.6f, 1.8f, speedFactor);
        moveClock += deltaTime * frequency;
        float wave = Mathf.Sin(moveClock);
        float pulse = Mathf.Sin(moveClock * 0.5f + 0.7f);

        float squash = Mathf.Max(0f, wave) * moveSquashAmount;
        float stretch = Mathf.Max(0f, -wave) * moveStretchAmount;
        float xScale = 1f + squash - stretch * 0.45f;
        float yScale = 1f - squash + stretch;
        float zScale = 1f;
        ApplyVisualScaleAndGrounding(xScale, yScale, zScale);

        float bob = Mathf.Abs(pulse) * moveBobAmount * Mathf.Lerp(0.5f, 1f, speedFactor);
        visualRoot.localPosition += Vector3.up * bob;

        float targetTilt = -currentMoveDirection.x * moveTiltAmount * Mathf.Lerp(0.25f, 1f, speedFactor);
        Quaternion desiredRotation = Quaternion.Euler(
            baseVisualLocalRotation.eulerAngles.x,
            baseVisualLocalRotation.eulerAngles.y,
            baseVisualLocalRotation.eulerAngles.z + targetTilt);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, desiredRotation, deltaTime * 10f);
    }

    private void RestoreVisualPose(float blendSpeed)
    {
        if (visualRoot == null)
        {
            return;
        }

        DissolveOnDeath dissolveOnDeath = GetComponent<DissolveOnDeath>();
        if (dissolveOnDeath != null && dissolveOnDeath.IsDeathStarted)
        {
            return;
        }

        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, baseVisualLocalScale, blendSpeed);
        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, baseVisualLocalPosition, blendSpeed);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, baseVisualLocalRotation, blendSpeed);
        SetVisualAlpha(Mathf.Lerp(GetVisualAlpha(), baseVisualColor.a, blendSpeed));
    }

    private void ApplyVisualScaleAndGrounding(float xMul, float yMul, float zMul)
    {
        if (visualRoot == null)
        {
            return;
        }

        Vector3 newScale = new Vector3(
            baseVisualLocalScale.x * xMul,
            baseVisualLocalScale.y * yMul,
            baseVisualLocalScale.z * zMul);
        visualRoot.localScale = newScale;

        float yRatio = Mathf.Abs(baseVisualLocalScale.y) > 0.0001f
            ? newScale.y / baseVisualLocalScale.y
            : 1f;

        float groundingOffset = -(yRatio - 1f) * spriteHalfHeightLocal;
        visualRoot.localPosition = baseVisualLocalPosition + new Vector3(0f, groundingOffset, 0f);
    }

    private void PlayAttackLandingVfx(Vector3 landingPosition, float jumpDistance)
    {
        if (!enableAttackLandingVfx || attackLandingVfxPrefab == null)
        {
            return;
        }

        bool hasVisibleJump =
            Mathf.Max(0f, attackJumpHeight) > 0.05f ||
            (allowAttackForwardLeap && jumpDistance > 0.05f);
        if (!hasVisibleJump)
        {
            return;
        }

        EnemyLandingVfxUtility.PlayLandingVfx(
            attackLandingVfxPrefab,
            landingPosition,
            attackLandingVfxOffset,
            attackLandingVfxLifetime,
            Quaternion.identity);
    }

    private void ResolveVisualReferences()
    {
        if (autoBindVisualByName && visualRoot == null)
        {
            Transform namedVisual = transform.Find("Visual");
            if (namedVisual != null)
            {
                visualRoot = namedVisual;
            }
        }

        if (slimeSpriteRenderer == null && visualRoot != null)
        {
            slimeSpriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
        }

        if (slimeSpriteRenderer == null)
        {
            slimeSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (slimeSpriteRenderer == null)
        {
            slimeSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (visualRoot == null && slimeSpriteRenderer != null)
        {
            visualRoot = slimeSpriteRenderer.transform;
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (slimeSpriteRenderer == null)
        {
            Debug.LogWarning("SlimeAnimationController could not find SpriteRenderer. Create a Visual child with SpriteRenderer or add SpriteRenderer on Enemy_Slime.", this);
        }
    }

    private void CaptureVisualBaseState()
    {
        if (visualRoot == null)
        {
            return;
        }

        baseVisualLocalScale = visualRoot.localScale;
        baseVisualLocalPosition = visualRoot.localPosition;
        baseVisualLocalRotation = visualRoot.localRotation;

        if (slimeSpriteRenderer != null)
        {
            baseVisualColor = slimeSpriteRenderer.color;
            if (slimeSpriteRenderer.sprite != null)
            {
                spriteHalfHeightLocal = slimeSpriteRenderer.sprite.bounds.size.y * Mathf.Abs(baseVisualLocalScale.y) * 0.5f;
            }
        }
    }

    private void CacheOwnerReferences()
    {
        if (ownerRigidbody == null)
        {
            ownerRigidbody = GetComponent<Rigidbody>();
        }

        if (ownerEnemyController == null)
        {
            ownerEnemyController = GetComponent<EnemyController>();
        }
    }

    private void UpdateFacing()
    {
        if (!enableAutoFlip || slimeSpriteRenderer == null)
        {
            return;
        }

        CacheOwnerReferences();

        if (TryResolveFacingFromMoveDirection(out bool moveFacingRight))
        {
            ApplyFacing(moveFacingRight, "MoveDirection");
            return;
        }

        if (TryResolveFacingFromTarget(out bool targetFacingRight))
        {
            ApplyFacing(targetFacingRight, "Target");
        }
    }

    private bool TryResolveFacingFromMoveDirection(out bool desiredFacingRight)
    {
        desiredFacingRight = facingRight;
        if (ownerRigidbody == null)
        {
            return false;
        }

        float horizontalVelocity = ownerRigidbody.linearVelocity.x;
        if (Mathf.Abs(horizontalVelocity) <= Mathf.Max(0f, facingDeadZone))
        {
            return false;
        }

        desiredFacingRight = horizontalVelocity > 0f;
        return true;
    }

    private bool TryResolveFacingFromTarget(out bool desiredFacingRight)
    {
        desiredFacingRight = facingRight;
        Transform target = ownerEnemyController != null ? ownerEnemyController.CurrentTarget : null;
        if (target == null)
        {
            return false;
        }

        float deltaX = target.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= Mathf.Max(0f, facingDeadZone))
        {
            return false;
        }

        desiredFacingRight = deltaX > 0f;
        return true;
    }

    private void ApplyFacing(bool desiredFacingRight, string source)
    {
        if (hasFacingDirection && facingRight == desiredFacingRight)
        {
            return;
        }

        facingRight = desiredFacingRight;
        hasFacingDirection = true;
        slimeSpriteRenderer.flipX = spriteDefaultFacesLeft ? facingRight : !facingRight;

        if (debugFacing)
        {
            Debug.Log($"[SlimeFacing] name={name} facingRight={facingRight} source={source}", this);
        }
    }

    private void EnsureDeathParticles()
    {
        if (deathParticles != null)
        {
            return;
        }

        GameObject particlesObject = new GameObject("SlimeDeathParticles");
        particlesObject.transform.SetParent(transform, false);
        particlesObject.transform.localPosition = Vector3.zero;
        deathParticles = particlesObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = deathParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startSpeed = deathParticleSpeed;
        main.maxParticles = Mathf.Max(8, deathParticleCount * 2);
        main.gravityModifier = -0.02f;

        ParticleSystem.EmissionModule emission = deathParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = deathParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = deathParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(deathParticleColor, 0f),
                new GradientColorKey(Color.Lerp(deathParticleColor, Color.white, 0.75f), 0.65f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = deathParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.55f, 1.35f);
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
    }

    private void EmitDeathParticles()
    {
        EnsureDeathParticles();
        if (deathParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = deathParticles.main;
        main.startSpeed = deathParticleSpeed;

        int emitCount = Mathf.Clamp(deathParticleCount, 4, 64);
        deathParticles.Emit(emitCount);
    }

    private void SetVisualAlpha(float alpha)
    {
        if (slimeSpriteRenderer == null)
        {
            return;
        }

        DissolveOnDeath dissolveOnDeath = GetComponent<DissolveOnDeath>();
        if (dissolveOnDeath != null && dissolveOnDeath.IsDeathFinished)
        {
            return;
        }

        Color color = slimeSpriteRenderer.color;
        float clampedAlpha = Mathf.Clamp01(alpha);
        if (!isDying)
        {
            clampedAlpha = Mathf.Max(clampedAlpha, Mathf.Clamp01(baseVisualColor.a * minimumVisibleAlpha));
        }

        color.a = clampedAlpha;
        slimeSpriteRenderer.color = color;
    }

    private float GetVisualAlpha()
    {
        if (slimeSpriteRenderer == null)
        {
            return 1f;
        }

        return slimeSpriteRenderer.color.a;
    }

    private void RaiseAttackHit(Transform target)
    {
        OnAttackHit?.Invoke(target);
        onAttackHit?.Invoke(target);
    }

    private void HookHealthEvents()
    {
        if (!autoPlayDeathOnHealthEvent)
        {
            return;
        }

        combatHealth = GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            previousCombatDestroyOnDeath = combatHealth.destroyOnDeath;
            combatHealth.destroyOnDeath = false;
            combatHealth.Died += OnHealthDied;
        }

        hookedHealth = true;
    }

    private void UnhookHealthEvents()
    {
        if (!hookedHealth)
        {
            return;
        }

        if (combatHealth != null)
        {
            combatHealth.Died -= OnHealthDied;
            if (!isDying)
            {
                combatHealth.destroyOnDeath = previousCombatDestroyOnDeath;
            }
        }

        hookedHealth = false;
    }

    private void OnHealthDied(GameObject killer)
    {
        PlayDeath();
    }

    private static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        float inv = 1f - x;
        return 1f - inv * inv * inv;
    }
}


