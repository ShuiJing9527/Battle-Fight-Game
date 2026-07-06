using System;
using UnityEngine;

public static class Player01RFourNeedleUtility
{
    private static readonly Vector2[] SectorSigns =
    {
        new Vector2(-1f, 1f),
        new Vector2(1f, 1f),
        new Vector2(-1f, -1f),
        new Vector2(1f, -1f)
    };

    [Serializable]
    public struct SpawnSettings
    {
        public int needleCount;
        public float spawnRadiusMin;
        public float spawnRadiusMax;
        public float heightMin;
        public float heightMax;
        public float horizontalRandomAngle;
        public bool enforceMinVisibleAngle;
        public float minVisibleAngle;
        public Camera viewCamera;
    }

    public static Vector3[] BuildSpawnPositions(
        Vector3 targetCenter,
        Vector3 referenceForward,
        SpawnSettings settings,
        Func<float, float, float> nextRange)
    {
        int count = Mathf.Max(1, settings.needleCount);
        Vector3[] result = new Vector3[count];
        Vector3 planarForward = Vector3.ProjectOnPlane(referenceForward, Vector3.up);
        if (planarForward.sqrMagnitude <= 0.0001f)
        {
            planarForward = Vector3.forward;
        }

        planarForward.Normalize();
        Vector3 planarRight = Vector3.Cross(Vector3.up, planarForward).normalized;
        int sectorCount = SectorSigns.Length;

        for (int i = 0; i < count; i++)
        {
            int sectorIndex = i % sectorCount;
            Vector2 sectorSign = SectorSigns[sectorIndex];
            Vector3 sectorBase = (planarRight * sectorSign.x + planarForward * sectorSign.y).normalized;
            result[i] = ResolveSpawnPosition(targetCenter, sectorBase, sectorSign, settings, nextRange);
        }

        return result;
    }

    public static Color GetSectorColor(int sectorIndex)
    {
        switch (sectorIndex)
        {
            case 0:
                return new Color(0.25f, 0.95f, 1f, 0.9f);
            case 1:
                return new Color(0.45f, 1f, 0.8f, 0.9f);
            case 2:
                return new Color(0.4f, 0.8f, 1f, 0.9f);
            default:
                return new Color(0.65f, 0.95f, 1f, 0.9f);
        }
    }

    public static Vector2 GetSectorSign(int sectorIndex)
    {
        int safeIndex = Mathf.Abs(sectorIndex) % SectorSigns.Length;
        return SectorSigns[safeIndex];
    }

    private static Vector3 ResolveSpawnPosition(
        Vector3 targetCenter,
        Vector3 sectorBase,
        Vector2 sectorSign,
        SpawnSettings settings,
        Func<float, float, float> nextRange)
    {
        float radiusLow = Mathf.Min(settings.spawnRadiusMin, settings.spawnRadiusMax);
        float radiusHigh = Mathf.Max(settings.spawnRadiusMin, settings.spawnRadiusMax);
        float heightLow = Mathf.Min(settings.heightMin, settings.heightMax);
        float heightHigh = Mathf.Max(settings.heightMin, settings.heightMax);

        Camera mainCamera = settings.viewCamera;
        Vector3 bestSpawn = targetCenter + sectorBase * radiusHigh + Vector3.up * Mathf.Max(0f, heightLow);
        float bestDot = 1f;

        for (int attempt = 0; attempt < 6; attempt++)
        {
            float signedAngle = nextRange != null
                ? nextRange(-settings.horizontalRandomAngle, settings.horizontalRandomAngle)
                : 0f;
            Vector3 direction = Quaternion.AngleAxis(signedAngle, Vector3.up) * sectorBase;
            float radius = nextRange != null ? nextRange(radiusLow, radiusHigh) : radiusHigh;
            float height = nextRange != null ? nextRange(heightLow, heightHigh) : heightHigh;

            Vector3 spawn = targetCenter + direction * radius + Vector3.up * Mathf.Max(0f, height);
            spawn.y = Mathf.Max(spawn.y, targetCenter.y - 0.05f);

            if (!settings.enforceMinVisibleAngle || mainCamera == null)
            {
                return spawn;
            }

            Vector3 launchDirection = (targetCenter - spawn).normalized;
            float alignment = Mathf.Abs(Vector3.Dot(launchDirection, mainCamera.transform.forward.normalized));
            if (alignment < bestDot)
            {
                bestDot = alignment;
                bestSpawn = spawn;
            }

            float limitDot = Mathf.Cos(settings.minVisibleAngle * Mathf.Deg2Rad);
            if (alignment <= limitDot)
            {
                return spawn;
            }
        }

        if (settings.enforceMinVisibleAngle && mainCamera != null)
        {
            Vector3 cameraRight = mainCamera.transform.right;
            Vector3 sideOffset = Vector3.ProjectOnPlane(cameraRight, Vector3.up).normalized;
            if (sideOffset.sqrMagnitude > 0.0001f)
            {
                float extraRadius = Mathf.Lerp(radiusLow, radiusHigh, 0.35f);
                bestSpawn += sideOffset * (sectorSign.x * extraRadius * 0.25f);
            }

            bestSpawn.y = Mathf.Max(bestSpawn.y, targetCenter.y - 0.05f);
        }

        return bestSpawn;
    }
}
