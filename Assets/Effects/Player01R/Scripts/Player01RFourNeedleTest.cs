using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class Player01RFourNeedleTest : MonoBehaviour
{
    [Header("Test Setup")]
    [SerializeField] private Player01REnergyNeedle needlePrefab;
    [SerializeField] private Transform targetTransform;
    [SerializeField, Min(1)] private int needleCount = 4;

    [Header("Spawn Volume")]
    [SerializeField, Min(0.1f)] private float spawnRadiusMin = 3.5f;
    [SerializeField, Min(0.1f)] private float spawnRadiusMax = 5.5f;
    [SerializeField] private float heightMin = 0.5f;
    [SerializeField] private float heightMax = 2.2f;
    [SerializeField] private float targetHeightOffset = 0.8f;
    [SerializeField, Range(0f, 60f)] private float horizontalRandomAngle = 18f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float launchInterval = 0.12f;
    [SerializeField, Min(0.01f)] private float travelSpeed = 38f;
    [SerializeField, Min(0f)] private float passThroughDistance = 4.5f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;

    [Header("View Safety")]
    [SerializeField] private bool enforceMinVisibleAngle = true;
    [SerializeField, Range(0f, 45f)] private float minVisibleAngle = 12f;

    [Header("Random")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int randomSeed = 101;

    [Header("Input")]
    [SerializeField] private KeyCode testKey = KeyCode.Y;

    private Coroutine fireRoutine;
    private readonly List<Player01REnergyNeedle> activeNeedles = new List<Player01REnergyNeedle>();

    private Random randomGenerator;

    private void Update()
    {
        if (!Input.GetKeyDown(testKey))
        {
            return;
        }

        FireFourNeedles();
    }

    [ContextMenu("Fire Four Needles")]
    public void FireFourNeedles()
    {
        if (needlePrefab == null || targetTransform == null)
        {
            Debug.LogWarning("[Player01 R Four Needle Test] Missing needle prefab or target transform.", this);
            return;
        }

        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
        }

        CleanupDeadNeedles();
        fireRoutine = StartCoroutine(FireSequenceRoutine());
    }

    private IEnumerator FireSequenceRoutine()
    {
        randomGenerator = useRandomSeed
            ? new Random(System.Environment.TickCount ^ GetInstanceID())
            : new Random(randomSeed);

        Vector3 targetCenter = GetTargetCenter();
        int clampedCount = Mathf.Max(1, needleCount);
        Vector3[] spawnPositions = BuildSpawnPositions(targetCenter, clampedCount);

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            SpawnNeedle(spawnPositions[i], targetCenter, i);
            if (i < spawnPositions.Length - 1 && launchInterval > 0f)
            {
                yield return new WaitForSeconds(launchInterval);
            }
        }

        fireRoutine = null;
    }

    private void SpawnNeedle(Vector3 spawnPosition, Vector3 targetCenter, int index)
    {
        Player01REnergyNeedle needle = Instantiate(needlePrefab, spawnPosition, Quaternion.identity);
        needle.name = needlePrefab.name + "_FourNeedleTest_" + index;
        needle.Launch(
            spawnPosition,
            targetCenter,
            travelSpeed,
            passThroughDistance,
            fadeDuration);

        activeNeedles.Add(needle);
    }

    private Vector3[] BuildSpawnPositions(Vector3 targetCenter, int count)
    {
        Player01RFourNeedleUtility.SpawnSettings settings = new Player01RFourNeedleUtility.SpawnSettings
        {
            needleCount = count,
            spawnRadiusMin = spawnRadiusMin,
            spawnRadiusMax = spawnRadiusMax,
            heightMin = heightMin,
            heightMax = heightMax,
            horizontalRandomAngle = horizontalRandomAngle,
            enforceMinVisibleAngle = enforceMinVisibleAngle,
            minVisibleAngle = minVisibleAngle,
            viewCamera = Camera.main
        };

        return Player01RFourNeedleUtility.BuildSpawnPositions(
            targetCenter,
            GetPlanarForward(),
            settings,
            NextRange);
    }

    private float NextRange(float min, float max)
    {
        if (randomGenerator == null)
        {
            randomGenerator = useRandomSeed
                ? new Random(System.Environment.TickCount ^ GetInstanceID())
                : new Random(randomSeed);
        }

        if (Mathf.Approximately(min, max))
        {
            return min;
        }

        double value = randomGenerator.NextDouble();
        return Mathf.Lerp(min, max, (float)value);
    }

    private Vector3 GetTargetCenter()
    {
        if (targetTransform == null)
        {
            return transform.position + Vector3.up * targetHeightOffset;
        }

        return targetTransform.position + Vector3.up * targetHeightOffset;
    }

    private Vector3 GetPlanarForward()
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude <= 0.0001f)
        {
            planarForward = Vector3.forward;
        }

        return planarForward.normalized;
    }

    private void CleanupDeadNeedles()
    {
        for (int i = activeNeedles.Count - 1; i >= 0; i--)
        {
            if (activeNeedles[i] == null)
            {
                activeNeedles.RemoveAt(i);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 targetCenter = GetTargetCenter();
        Vector3 planarForward = GetPlanarForward();
        Vector3 planarRight = Vector3.Cross(Vector3.up, planarForward).normalized;

        Gizmos.color = new Color(0.2f, 1f, 1f, 0.95f);
        Gizmos.DrawSphere(targetCenter, 0.08f);
        Gizmos.DrawLine(targetCenter, targetCenter + Vector3.up * 0.45f);

        float radiusLow = Mathf.Min(spawnRadiusMin, spawnRadiusMax);
        float radiusHigh = Mathf.Max(spawnRadiusMin, spawnRadiusMax);
        float heightLow = Mathf.Min(heightMin, heightMax);
        float heightHigh = Mathf.Max(heightMin, heightMax);

        for (int i = 0; i < 4; i++)
        {
            Vector2 sectorSign = Player01RFourNeedleUtility.GetSectorSign(i);
            Vector3 sectorBase = (planarRight * sectorSign.x + planarForward * sectorSign.y).normalized;
            Vector3 innerPoint = targetCenter + sectorBase * radiusLow + Vector3.up * Mathf.Max(0f, heightLow);
            Vector3 outerPoint = targetCenter + sectorBase * radiusHigh + Vector3.up * Mathf.Max(0f, heightHigh);

            Gizmos.color = Player01RFourNeedleUtility.GetSectorColor(i);
            Gizmos.DrawLine(targetCenter, innerPoint);
            Gizmos.DrawLine(innerPoint, outerPoint);
            Gizmos.DrawWireSphere(innerPoint, 0.1f);
            Gizmos.DrawWireSphere(outerPoint, 0.14f);
        }
    }
}
