using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class Player1Skill_E_BrokenDash : Player01SkillBase
{
    [Header("E - 持续奔跑")]
    [SerializeField, Min(0.1f)] private float speedMultiplier = 2f;
    [SerializeField] private bool ignoreObstacleCollision = true;
    [SerializeField] private LayerMask obstacleLayers = 1 << 3;
    [Header("E - 幽灵视觉")]
    [SerializeField] private Player01GhostStateVisual eGhostStateVisual;
    [SerializeField] private bool eEnableGhostStateVisual = true;

    public bool IsRunningBoost { get; private set; }

    private PlayerMovement cachedMovement;
    private float cachedOriginalMoveSpeed = -1f;
    private readonly Dictionary<int, bool> cachedLayerCollisionStates = new Dictionary<int, bool>();

    private void Reset()
    {
        cooldown = 2.2f;
        duration = 3f;
        effectPower = 4f;
        animationName = "Run";
        debugLog = true;
        speedMultiplier = 2f;
        ignoreObstacleCollision = true;
        obstacleLayers = 1 << 3;
    }

    public override void Cast()
    {
        if (IsRunningBoost)
        {
            if (debugLog)
            {
                Debug.Log("[Player01 E Run] already running, ignored.", this);
            }

            return;
        }

        base.Cast();
    }

    private void Awake()
    {
        cachedMovement = GetComponent<PlayerMovement>();
        CacheGhostStateVisual();
    }

    protected override bool ShouldLoopAnimation()
    {
        return true;
    }

    protected override void OnCastStarted()
    {
        IsRunningBoost = true;
        ApplySpeedBoost();
        ApplyObstacleCollisionIgnore(true);
        SetGhostStateVisible(true);

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
        IsRunningBoost = false;
        RestoreSpeed();
        ApplyObstacleCollisionIgnore(false);
        SetGhostStateVisible(false);

        if (debugLog)
        {
            Debug.Log("[Player01 E Run] end restore movement/collision", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "E - 持续奔跑";
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
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SetGhostStateVisible(false);
    }

    private void CacheGhostStateVisual()
    {
        if (eGhostStateVisual != null)
        {
            return;
        }

        eGhostStateVisual = GetComponentInChildren<Player01GhostStateVisual>(true);
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
}
