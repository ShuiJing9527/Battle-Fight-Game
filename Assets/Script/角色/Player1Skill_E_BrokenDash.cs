using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class Player1Skill_E_BrokenDash : Player01SkillBase
{
    [Header("E")]
    [SerializeField, Min(0.1f)] private float speedMultiplier = 1.75f;
    [SerializeField] private bool ignoreObstacleCollision = true;
    [SerializeField] private LayerMask obstacleLayers = 1 << 3;

    public bool IsRunningBoost { get; private set; }

    private PlayerMovement cachedMovement;
    private float cachedOriginalMoveSpeed = -1f;
    private readonly Dictionary<int, bool> cachedLayerCollisionStates = new Dictionary<int, bool>();

    private void Reset()
    {
        cooldown = 2.2f;
        duration = 0.35f;
        effectPower = 4f;
        animationName = "Run";
        debugLog = true;
        speedMultiplier = 1.75f;
        ignoreObstacleCollision = true;
        obstacleLayers = 1 << 3;
    }

    private void Awake()
    {
        cachedMovement = GetComponent<PlayerMovement>();
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

        if (debugLog)
        {
            Debug.Log($"[E - BrokenDash] Start. animation={animationName}, speedMultiplier={speedMultiplier:F2}, ignoreObstacleCollision={ignoreObstacleCollision}", this);
        }

        PlayRunAnimation();
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
    }

    protected override string GetSkillLabel()
    {
        return "E - BrokenDash";
    }

    private void PlayRunAnimation()
    {
        if (Controller == null)
        {
            return;
        }

        float lockDuration = Mathf.Max(0f, duration);
        if (debugLog)
        {
            Debug.Log($"[E - BrokenDash] Try play {animationName} with lock={lockDuration:F2}.", this);
        }

        if (!Controller.TryPlayLockedSkillAnimation(animationName, true, lockDuration) && debugLog)
        {
            Debug.LogWarning($"[E - BrokenDash] Failed to play animation '{animationName}'.", this);
        }
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
}
