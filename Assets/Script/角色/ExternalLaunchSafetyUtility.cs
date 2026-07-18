using UnityEngine;

public static class ExternalLaunchSafetyUtility
{
    public static bool TryResolvePhysicalBodyCollider(Component owner, Rigidbody body, out Collider solidCollider)
    {
        solidCollider = null;
        if (owner == null || body == null)
        {
            return false;
        }

        Collider[] colliders = owner.GetComponentsInChildren<Collider>(true);
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null ||
                !collider.enabled ||
                collider.isTrigger ||
                collider.attachedRigidbody != body)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            Vector3 size = bounds.size;
            float volumeScore = size.x * size.y * size.z;
            if (volumeScore > bestScore)
            {
                bestScore = volumeScore;
                solidCollider = collider;
            }
        }

        return solidCollider != null;
    }

    public static float ResolveBottomOffset(Rigidbody body, Collider solidCollider)
    {
        if (body == null || solidCollider == null)
        {
            return 0f;
        }

        return body.position.y - solidCollider.bounds.min.y;
    }

    public static bool TryComputeLaunchStartGroundCorrection(
        float penetrationDepth,
        float maxCorrection,
        float groundSkin,
        out float correctionApplied,
        out bool severePenetration)
    {
        correctionApplied = 0f;
        severePenetration = false;

        if (penetrationDepth <= 0f)
        {
            return false;
        }

        float clampedMaxCorrection = Mathf.Max(0f, maxCorrection);
        if (penetrationDepth > clampedMaxCorrection)
        {
            severePenetration = true;
            return false;
        }

        correctionApplied = penetrationDepth + Mathf.Max(0f, groundSkin);
        return correctionApplied > 0f;
    }

    public static bool ShouldApplyRiseSafety(
        bool enableRiseSafety,
        bool riseSafetyApplied,
        int currentVerificationStep,
        int maxVerificationSteps,
        float deltaY,
        float minimumRiseDistance,
        float currentVelocityY,
        float minimumAcceptedUpwardVelocity)
    {
        if (!enableRiseSafety ||
            riseSafetyApplied ||
            currentVerificationStep > Mathf.Max(1, maxVerificationSteps))
        {
            return false;
        }

        bool noAcceptedRise = deltaY < Mathf.Max(0f, minimumRiseDistance);
        bool weakUpwardVelocity = currentVelocityY < Mathf.Max(0f, minimumAcceptedUpwardVelocity);
        return noAcceptedRise && weakUpwardVelocity;
    }
}
