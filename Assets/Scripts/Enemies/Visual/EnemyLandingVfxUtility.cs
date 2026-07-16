using UnityEngine;

public static class EnemyLandingVfxUtility
{
    public static GameObject PlayLandingVfx(
        GameObject prefab,
        Vector3 landingPosition,
        Vector3 offset,
        float lifetime,
        Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        Vector3 groundedPosition = ResolveGroundedPosition(landingPosition);
        GameObject instance = Object.Instantiate(prefab, groundedPosition + offset, rotation);
        if (instance != null && lifetime > 0f)
        {
            Object.Destroy(instance, lifetime);
        }

        return instance;
    }

    private static Vector3 ResolveGroundedPosition(Vector3 landingPosition)
    {
        Vector3 rayOrigin = landingPosition + Vector3.up * 1.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return landingPosition;
    }
}
