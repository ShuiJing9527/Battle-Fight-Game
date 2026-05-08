using System;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine;

namespace UnderTheStars.GenerationMap
{
    public enum AreaType
    {
        Grass,
        CoastTransition,
        Beach,
        Water,
        Rock,
        NoSpawn
    }

    [Serializable]
    public enum SpawnMode
    {
        Single,
        Cluster
    }

    [Serializable]
    public class PropSpawnRule
    {
        public List<GameObject> prefabs = new List<GameObject>();
        public SpawnMode spawnMode = SpawnMode.Single;
        [Min(0f)] public float spawnWeight = 1f;
        [Range(0f, 1f)] public float density = 0.15f;
        [Min(0f)] public float minDistance = 0.5f;
        [Min(0)] public int requiredGroundRadius = 0;
        public List<AreaType> allowedAreaTypes = new List<AreaType> { AreaType.Grass };
        public List<AreaType> forbiddenAreaTypes = new List<AreaType>();
        public Vector3 positionOffset = Vector3.zero;
        public Vector2 randomCellOffset = Vector2.zero;
        public float randomScaleMin = 1f;
        public float randomScaleMax = 1f;
        public bool randomFlipX = false;
        public bool randomRotationY = false;
        public bool randomRotationZ = true;
        [Min(0)] public int maxCount = 200;

        [Header("Cluster")]
        [Min(0)] public int clusterCountMin = 2;
        [Min(0)] public int clusterCountMax = 4;
        [Min(0f)] public float clusterRadiusMin = 3f;
        [Min(0f)] public float clusterRadiusMax = 6f;
        [Range(0f, 1f)] public float clusterDensity = 0.5f;
        [Min(0)] public int clusterMaxCount = 60;
        [Range(0f, 3f)] public float clusterFalloff = 1f;
    }

    [Serializable]
    public class PropGroupRule
    {
        public List<GameObject> groupCenterPrefabs = new List<GameObject>();
        public List<GameObject> aroundPrefabs = new List<GameObject>();
        [Min(0)] public int groupCountMin = 1;
        [Min(0)] public int groupCountMax = 3;
        [Min(0f)] public float groupRadiusMin = 2f;
        [Min(0f)] public float groupRadiusMax = 4f;
        [Min(0)] public int aroundCountMin = 3;
        [Min(0)] public int aroundCountMax = 8;
        public Vector3 centerOffset = Vector3.zero;
        public Vector3 aroundPositionOffset = Vector3.zero;
        public Vector2 randomCellOffset = Vector2.zero;
        [Min(0f)] public float minGroupDistance = 5f;
        [Min(0)] public int requiredGroundRadius = 0;
        public List<AreaType> allowedAreaTypes = new List<AreaType> { AreaType.Grass };
        public List<AreaType> forbiddenAreaTypes = new List<AreaType>();
        public float randomScaleMin = 1f;
        public float randomScaleMax = 1f;
        public bool randomFlipX = false;
    }

    public class RandomMapPainProp : MonoBehaviour
    {
        [Header("Spawn Rules")]
        [SerializeField] private List<PropSpawnRule> spawnRules = new List<PropSpawnRule>();
        [SerializeField] private List<PropGroupRule> groupRules = new List<PropGroupRule>();

        [Header("Container")]
        [SerializeField] private string propsRootName = "PropsRoot";

        private Transform propsRoot;

        internal void InitClearProp()
        {
            if (propsRoot == null)
            {
                Transform existing = transform.Find(propsRootName);
                if (existing != null)
                {
                    propsRoot = existing;
                }
            }

            if (propsRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(propsRoot.gameObject);
            }
            else
            {
                DestroyImmediate(propsRoot.gameObject);
            }

            propsRoot = null;
        }

        private readonly struct SpawnPoint
        {
            public readonly Vector2Int point;
            public readonly AreaType areaType;

            public SpawnPoint(Vector2Int point, AreaType areaType)
            {
                this.point = point;
                this.areaType = areaType;
            }
        }

        internal void SpawnProps(HashSet<Vector2Int>[,] floorPoints, Tilemap referenceTilemap, Dictionary<Vector2Int, AreaType> areaTypeByPoint)
        {
            if (floorPoints == null || referenceTilemap == null || spawnRules == null || spawnRules.Count == 0)
            {
                return;
            }

            EnsurePropsRoot();

            List<SpawnPoint> allFloorPoints = CollectFloorPoints(floorPoints, areaTypeByPoint);
            if (allFloorPoints.Count == 0)
            {
                return;
            }

            Shuffle(allFloorPoints);

            List<SpawnedPropRecord> spawnedRecords = new List<SpawnedPropRecord>();
            Dictionary<PropSpawnRule, int> spawnedCountPerRule = new Dictionary<PropSpawnRule, int>();
            Dictionary<Vector2Int, AreaType> areaLookup = BuildAreaLookup(allFloorPoints);

            List<PropSpawnRule> clusterRules = new List<PropSpawnRule>();
            List<PropSpawnRule> singleRules = new List<PropSpawnRule>();
            SplitRules(clusterRules, singleRules);

            SpawnGroups(groupRules, allFloorPoints, areaLookup, referenceTilemap, spawnedRecords);
            SpawnClusters(clusterRules, allFloorPoints, areaLookup, referenceTilemap, spawnedRecords, spawnedCountPerRule);
            SpawnSingles(singleRules, allFloorPoints, areaLookup, referenceTilemap, spawnedRecords, spawnedCountPerRule);
        }

        private void SpawnGroups(
            List<PropGroupRule> rules,
            List<SpawnPoint> allFloorPoints,
            Dictionary<Vector2Int, AreaType> areaLookup,
            Tilemap referenceTilemap,
            List<SpawnedPropRecord> spawnedRecords)
        {
            if (rules == null || rules.Count == 0 || allFloorPoints.Count == 0)
            {
                return;
            }

            List<Vector3> placedGroupCenters = new List<Vector3>();

            foreach (PropGroupRule rule in rules)
            {
                if (rule == null || !HasValidPrefabList(rule.groupCenterPrefabs) || !HasValidPrefabList(rule.aroundPrefabs))
                {
                    continue;
                }

                int groupMin = Mathf.Max(0, Mathf.Min(rule.groupCountMin, rule.groupCountMax));
                int groupMax = Mathf.Max(groupMin, Mathf.Max(rule.groupCountMin, rule.groupCountMax));
                int groupCount = UnityEngine.Random.Range(groupMin, groupMax + 1);

                List<SpawnPoint> centerCandidates = new List<SpawnPoint>();
                for (int i = 0; i < allFloorPoints.Count; i++)
                {
                    if (IsAreaAllowed(rule.allowedAreaTypes, rule.forbiddenAreaTypes, allFloorPoints[i].areaType))
                    {
                        centerCandidates.Add(allFloorPoints[i]);
                    }
                }

                Shuffle(centerCandidates);

                int built = 0;
                for (int i = 0; i < centerCandidates.Count && built < groupCount; i++)
                {
                    SpawnPoint centerPoint = centerCandidates[i];
                    if (!PassRequiredGround(
                            centerPoint.point,
                            rule.requiredGroundRadius,
                            areaLookup,
                            areaType => IsAreaAllowed(rule.allowedAreaTypes, rule.forbiddenAreaTypes, areaType)))
                    {
                        continue;
                    }

                    Vector3 centerTileWorld = referenceTilemap.GetCellCenterWorld(new Vector3Int(centerPoint.point.x, centerPoint.point.y, 0));
                    Vector3 centerSpawnPosition = centerTileWorld + rule.centerOffset;
                    centerSpawnPosition = ApplyRandomCellOffset(centerSpawnPosition, rule.randomCellOffset);

                    if (!PassGroupDistance(centerSpawnPosition, rule.minGroupDistance, placedGroupCenters))
                    {
                        continue;
                    }

                    GameObject centerPrefab = PickRandomPrefab(rule.groupCenterPrefabs);
                    if (centerPrefab == null)
                    {
                        continue;
                    }

                    Quaternion centerRotation = centerPrefab.transform.rotation;
                    GameObject centerObj = Instantiate(centerPrefab, centerSpawnPosition, centerRotation, propsRoot);
                    ApplyRandomTransform(centerObj.transform, rule.randomScaleMin, rule.randomScaleMax, rule.randomFlipX);
                    placedGroupCenters.Add(centerSpawnPosition);
                    spawnedRecords.Add(new SpawnedPropRecord(centerSpawnPosition, 0f));

                    float radiusMin = Mathf.Min(rule.groupRadiusMin, rule.groupRadiusMax);
                    float radiusMax = Mathf.Max(rule.groupRadiusMin, rule.groupRadiusMax);
                    float radius = UnityEngine.Random.Range(radiusMin, radiusMax);
                    float radiusSq = radius * radius;
                    int aroundMin = Mathf.Max(0, Mathf.Min(rule.aroundCountMin, rule.aroundCountMax));
                    int aroundMax = Mathf.Max(aroundMin, Mathf.Max(rule.aroundCountMin, rule.aroundCountMax));
                    int aroundCount = UnityEngine.Random.Range(aroundMin, aroundMax + 1);

                    List<SpawnPoint> aroundCandidates = new List<SpawnPoint>();
                    for (int p = 0; p < allFloorPoints.Count; p++)
                    {
                        SpawnPoint sp = allFloorPoints[p];
                        if (!IsAreaAllowed(rule.allowedAreaTypes, rule.forbiddenAreaTypes, sp.areaType))
                        {
                            continue;
                        }

                        Vector2 delta = sp.point - centerPoint.point;
                        if (delta.sqrMagnitude <= radiusSq)
                        {
                            aroundCandidates.Add(sp);
                        }
                    }

                    Shuffle(aroundCandidates);

                    int spawnedAround = 0;
                    for (int p = 0; p < aroundCandidates.Count && spawnedAround < aroundCount; p++)
                    {
                        SpawnPoint aroundPoint = aroundCandidates[p];
                        Vector3 aroundTileWorld = referenceTilemap.GetCellCenterWorld(new Vector3Int(aroundPoint.point.x, aroundPoint.point.y, 0));
                        Vector3 aroundSpawnPosition = aroundTileWorld + rule.aroundPositionOffset;
                        aroundSpawnPosition = ApplyRandomCellOffset(aroundSpawnPosition, rule.randomCellOffset);

                        if (!PassMinDistance(aroundSpawnPosition, 0.1f, spawnedRecords))
                        {
                            continue;
                        }

                        GameObject aroundPrefab = PickRandomPrefab(rule.aroundPrefabs);
                        if (aroundPrefab == null)
                        {
                            continue;
                        }

                        Quaternion aroundRotation = aroundPrefab.transform.rotation;
                        GameObject aroundObj = Instantiate(aroundPrefab, aroundSpawnPosition, aroundRotation, propsRoot);
                        ApplyRandomTransform(aroundObj.transform, rule.randomScaleMin, rule.randomScaleMax, rule.randomFlipX);
                        spawnedRecords.Add(new SpawnedPropRecord(aroundSpawnPosition, 0f));
                        spawnedAround++;
                    }

                    built++;
                }
            }
        }

        private void SplitRules(List<PropSpawnRule> clusterRules, List<PropSpawnRule> singleRules)
        {
            foreach (PropSpawnRule rule in spawnRules)
            {
                if (rule == null)
                {
                    continue;
                }

                if (rule.spawnMode == SpawnMode.Cluster)
                {
                    clusterRules.Add(rule);
                }
                else
                {
                    singleRules.Add(rule);
                }
            }
        }

        private void SpawnSingles(
            List<PropSpawnRule> singleRules,
            List<SpawnPoint> allFloorPoints,
            Dictionary<Vector2Int, AreaType> areaLookup,
            Tilemap referenceTilemap,
            List<SpawnedPropRecord> spawnedRecords,
            Dictionary<PropSpawnRule, int> spawnedCountPerRule)
        {
            if (singleRules.Count == 0)
            {
                return;
            }

            foreach (SpawnPoint spawnPoint in allFloorPoints)
            {
                List<PropSpawnRule> candidates = GetCandidateRules(singleRules, spawnPoint.areaType, spawnedCountPerRule);
                if (candidates.Count == 0)
                {
                    continue;
                }

                PropSpawnRule selectedRule = PickWeightedRule(candidates);
                if (selectedRule == null)
                {
                    continue;
                }

                if (UnityEngine.Random.value > selectedRule.density)
                {
                    continue;
                }

                if (!PassRequiredGround(
                        spawnPoint.point,
                        selectedRule.requiredGroundRadius,
                        areaLookup,
                        areaType => IsAreaAllowed(selectedRule, areaType)))
                {
                    continue;
                }

                Vector3 worldPos = referenceTilemap.GetCellCenterWorld(new Vector3Int(spawnPoint.point.x, spawnPoint.point.y, 0));
                Vector3 spawnPosition = worldPos + selectedRule.positionOffset;
                spawnPosition = ApplyRandomCellOffset(spawnPosition, selectedRule.randomCellOffset);
                if (!PassMinDistance(spawnPosition, selectedRule.minDistance, spawnedRecords))
                {
                    continue;
                }

                GameObject selectedPrefab = PickRandomPrefab(selectedRule);
                if (selectedPrefab == null)
                {
                    continue;
                }

                Quaternion spawnRotation = selectedPrefab.transform.rotation;
                GameObject instance = Instantiate(selectedPrefab, spawnPosition, spawnRotation, propsRoot);
                ApplyRandomTransform(instance.transform, selectedRule);

                spawnedRecords.Add(new SpawnedPropRecord(spawnPosition, selectedRule.minDistance));
                spawnedCountPerRule[selectedRule] = GetSpawnCount(selectedRule, spawnedCountPerRule) + 1;
            }
        }

        private void SpawnClusters(
            List<PropSpawnRule> clusterRules,
            List<SpawnPoint> allFloorPoints,
            Dictionary<Vector2Int, AreaType> areaLookup,
            Tilemap referenceTilemap,
            List<SpawnedPropRecord> spawnedRecords,
            Dictionary<PropSpawnRule, int> spawnedCountPerRule)
        {
            if (clusterRules.Count == 0 || allFloorPoints.Count == 0)
            {
                return;
            }

            foreach (PropSpawnRule rule in clusterRules)
            {
                if (!HasValidPrefabs(rule) || rule.spawnWeight <= 0f || rule.maxCount == 0 || rule.clusterMaxCount == 0)
                {
                    continue;
                }

                int clustersMin = Mathf.Max(0, Mathf.Min(rule.clusterCountMin, rule.clusterCountMax));
                int clustersMax = Mathf.Max(clustersMin, Mathf.Max(rule.clusterCountMin, rule.clusterCountMax));
                int clusterCount = UnityEngine.Random.Range(clustersMin, clustersMax + 1);

                for (int clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
                {
                    int alreadySpawned = GetSpawnCount(rule, spawnedCountPerRule);
                    if (rule.maxCount > 0 && alreadySpawned >= rule.maxCount)
                    {
                        break;
                    }

                    List<SpawnPoint> centerCandidates = new List<SpawnPoint>();
                    for (int i = 0; i < allFloorPoints.Count; i++)
                    {
                        if (IsAreaAllowed(rule, allFloorPoints[i].areaType))
                        {
                            centerCandidates.Add(allFloorPoints[i]);
                        }
                    }
                    if (centerCandidates.Count == 0)
                    {
                        break;
                    }

                    SpawnPoint center = centerCandidates[UnityEngine.Random.Range(0, centerCandidates.Count)];
                    float radiusMin = Mathf.Min(rule.clusterRadiusMin, rule.clusterRadiusMax);
                    float radiusMax = Mathf.Max(rule.clusterRadiusMin, rule.clusterRadiusMax);
                    float radius = UnityEngine.Random.Range(radiusMin, radiusMax);
                    float radiusSq = radius * radius;
                    int clusterSpawned = 0;

                    List<SpawnPoint> candidates = new List<SpawnPoint>();
                    for (int i = 0; i < allFloorPoints.Count; i++)
                    {
                        SpawnPoint point = allFloorPoints[i];
                        Vector2 delta = point.point - center.point;
                        if (delta.sqrMagnitude <= radiusSq)
                        {
                            candidates.Add(point);
                        }
                    }

                    Shuffle(candidates);

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (clusterSpawned >= rule.clusterMaxCount)
                        {
                            break;
                        }

                        if (rule.maxCount > 0 && GetSpawnCount(rule, spawnedCountPerRule) >= rule.maxCount)
                        {
                            break;
                        }

                        SpawnPoint point = candidates[i];
                        if (!IsAreaAllowed(rule, point.areaType))
                        {
                            continue;
                        }
                        if (!PassRequiredGround(
                                point.point,
                                rule.requiredGroundRadius,
                                areaLookup,
                                areaType => IsAreaAllowed(rule, areaType)))
                        {
                            continue;
                        }

                        Vector2 delta = point.point - center.point;
                        float normalizedDistance = radius > 0f ? Mathf.Clamp01(delta.magnitude / radius) : 0f;
                        float falloffFactor = Mathf.Pow(1f - normalizedDistance, Mathf.Max(0f, rule.clusterFalloff));
                        float spawnChance = Mathf.Clamp01(rule.clusterDensity * falloffFactor);
                        if (UnityEngine.Random.value > spawnChance)
                        {
                            continue;
                        }

                        Vector3 worldPos = referenceTilemap.GetCellCenterWorld(new Vector3Int(point.point.x, point.point.y, 0));
                        Vector3 spawnPosition = worldPos + rule.positionOffset;
                        spawnPosition = ApplyRandomCellOffset(spawnPosition, rule.randomCellOffset);
                        if (!PassMinDistance(spawnPosition, rule.minDistance, spawnedRecords))
                        {
                            continue;
                        }

                        GameObject selectedPrefab = PickRandomPrefab(rule);
                        if (selectedPrefab == null)
                        {
                            continue;
                        }

                        Quaternion spawnRotation = selectedPrefab.transform.rotation;
                        GameObject instance = Instantiate(selectedPrefab, spawnPosition, spawnRotation, propsRoot);
                        ApplyRandomTransform(instance.transform, rule);
                        spawnedRecords.Add(new SpawnedPropRecord(spawnPosition, rule.minDistance));
                        spawnedCountPerRule[rule] = GetSpawnCount(rule, spawnedCountPerRule) + 1;
                        clusterSpawned++;
                    }
                }
            }
        }

        private void EnsurePropsRoot()
        {
            if (propsRoot != null)
            {
                return;
            }

            GameObject root = new GameObject(propsRootName);
            propsRoot = root.transform;
            propsRoot.SetParent(transform, false);
        }

        private static List<SpawnPoint> CollectFloorPoints(HashSet<Vector2Int>[,] floorPoints, Dictionary<Vector2Int, AreaType> areaTypeByPoint)
        {
            Dictionary<Vector2Int, AreaType> merged = new Dictionary<Vector2Int, AreaType>();
            for (int x = 0; x < floorPoints.GetLength(0); x++)
            {
                for (int y = 0; y < floorPoints.GetLength(1); y++)
                {
                    if (floorPoints[x, y] == null)
                    {
                        continue;
                    }

                    foreach (Vector2Int p in floorPoints[x, y])
                    {
                        if (!merged.ContainsKey(p))
                        {
                            if (areaTypeByPoint != null && areaTypeByPoint.TryGetValue(p, out AreaType areaType))
                            {
                                merged[p] = areaType;
                            }
                            else
                            {
                                merged[p] = AreaType.Grass;
                            }
                        }
                    }
                }
            }

            List<SpawnPoint> result = new List<SpawnPoint>(merged.Count);
            foreach (KeyValuePair<Vector2Int, AreaType> kv in merged)
            {
                result.Add(new SpawnPoint(kv.Key, kv.Value));
            }
            return result;
        }

        private static Dictionary<Vector2Int, AreaType> BuildAreaLookup(List<SpawnPoint> points)
        {
            Dictionary<Vector2Int, AreaType> lookup = new Dictionary<Vector2Int, AreaType>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                lookup[points[i].point] = points[i].areaType;
            }

            return lookup;
        }

        private List<PropSpawnRule> GetCandidateRules(List<PropSpawnRule> sourceRules, AreaType areaType, Dictionary<PropSpawnRule, int> spawnedCountPerRule)
        {
            List<PropSpawnRule> rules = new List<PropSpawnRule>();
            foreach (PropSpawnRule rule in sourceRules)
            {
                if (rule == null || !HasValidPrefabs(rule) || rule.spawnWeight <= 0f || rule.maxCount == 0)
                {
                    continue;
                }

                if (!IsAreaAllowed(rule, areaType))
                {
                    continue;
                }

                int currentCount = GetSpawnCount(rule, spawnedCountPerRule);
                if (rule.maxCount > 0 && currentCount >= rule.maxCount)
                {
                    continue;
                }

                rules.Add(rule);
            }

            return rules;
        }

        private static bool IsAreaAllowed(PropSpawnRule rule, AreaType areaType)
        {
            if (rule.allowedAreaTypes != null && rule.allowedAreaTypes.Count > 0 && !rule.allowedAreaTypes.Contains(areaType))
            {
                return false;
            }

            if (rule.forbiddenAreaTypes != null && rule.forbiddenAreaTypes.Contains(areaType))
            {
                return false;
            }

            return true;
        }

        private static bool IsAreaAllowed(List<AreaType> allowedAreaTypes, List<AreaType> forbiddenAreaTypes, AreaType areaType)
        {
            if (allowedAreaTypes != null && allowedAreaTypes.Count > 0 && !allowedAreaTypes.Contains(areaType))
            {
                return false;
            }

            if (forbiddenAreaTypes != null && forbiddenAreaTypes.Contains(areaType))
            {
                return false;
            }

            return true;
        }

        private static bool HasValidPrefabs(PropSpawnRule rule)
        {
            if (rule.prefabs == null || rule.prefabs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < rule.prefabs.Count; i++)
            {
                if (rule.prefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject PickRandomPrefab(PropSpawnRule rule)
        {
            if (rule.prefabs == null || rule.prefabs.Count == 0)
            {
                return null;
            }

            List<GameObject> validPrefabs = new List<GameObject>();
            for (int i = 0; i < rule.prefabs.Count; i++)
            {
                if (rule.prefabs[i] != null)
                {
                    validPrefabs.Add(rule.prefabs[i]);
                }
            }

            if (validPrefabs.Count == 0)
            {
                return null;
            }

            return validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
        }

        private static bool HasValidPrefabList(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject PickRandomPrefab(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                return null;
            }

            List<GameObject> validPrefabs = new List<GameObject>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null)
                {
                    validPrefabs.Add(prefabs[i]);
                }
            }

            if (validPrefabs.Count == 0)
            {
                return null;
            }

            return validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
        }

        private static int GetSpawnCount(PropSpawnRule rule, Dictionary<PropSpawnRule, int> spawnedCountPerRule)
        {
            return spawnedCountPerRule.TryGetValue(rule, out int count) ? count : 0;
        }

        private static PropSpawnRule PickWeightedRule(List<PropSpawnRule> rules)
        {
            float totalWeight = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                totalWeight += Mathf.Max(0f, rules[i].spawnWeight);
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float value = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < rules.Count; i++)
            {
                value -= Mathf.Max(0f, rules[i].spawnWeight);
                if (value <= 0f)
                {
                    return rules[i];
                }
            }

            return rules[rules.Count - 1];
        }

        private static bool PassMinDistance(Vector3 worldPos, float minDistance, List<SpawnedPropRecord> spawnedRecords)
        {
            float minDistSq = minDistance * minDistance;
            for (int i = 0; i < spawnedRecords.Count; i++)
            {
                float required = Mathf.Max(minDistance, spawnedRecords[i].minDistance);
                float requiredSq = required * required;
                if ((spawnedRecords[i].position - worldPos).sqrMagnitude < Mathf.Max(minDistSq, requiredSq))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PassGroupDistance(Vector3 centerWorldPos, float minGroupDistance, List<Vector3> placedCenters)
        {
            float minDistSq = minGroupDistance * minGroupDistance;
            for (int i = 0; i < placedCenters.Count; i++)
            {
                if ((placedCenters[i] - centerWorldPos).sqrMagnitude < minDistSq)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PassRequiredGround(
            Vector2Int centerPoint,
            int requiredGroundRadius,
            Dictionary<Vector2Int, AreaType> areaLookup,
            Func<AreaType, bool> areaPredicate)
        {
            if (requiredGroundRadius <= 0)
            {
                return true;
            }

            int radiusSq = requiredGroundRadius * requiredGroundRadius;
            for (int x = -requiredGroundRadius; x <= requiredGroundRadius; x++)
            {
                for (int y = -requiredGroundRadius; y <= requiredGroundRadius; y++)
                {
                    if ((x * x + y * y) > radiusSq)
                    {
                        continue;
                    }

                    Vector2Int point = new Vector2Int(centerPoint.x + x, centerPoint.y + y);
                    if (!areaLookup.TryGetValue(point, out AreaType areaType))
                    {
                        return false;
                    }

                    if (areaPredicate != null && !areaPredicate(areaType))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static Vector3 ApplyRandomCellOffset(Vector3 position, Vector2 offset)
        {
            float dx = UnityEngine.Random.Range(-Mathf.Abs(offset.x), Mathf.Abs(offset.x));
            float dz = UnityEngine.Random.Range(-Mathf.Abs(offset.y), Mathf.Abs(offset.y));
            position.x += dx;
            position.z += dz;
            return position;
        }

        private static void ApplyRandomTransform(Transform target, PropSpawnRule rule)
        {
            ApplyRandomTransform(target, rule.randomScaleMin, rule.randomScaleMax, rule.randomFlipX);
        }

        private static void ApplyRandomTransform(Transform target, float randomScaleMin, float randomScaleMax, bool randomFlipX)
        {
            Vector3 scale = target.localScale;
            float minScale = Mathf.Min(randomScaleMin, randomScaleMax);
            float maxScale = Mathf.Max(randomScaleMin, randomScaleMax);
            float uniformScale = UnityEngine.Random.Range(minScale, maxScale);
            scale *= uniformScale;

            if (randomFlipX && UnityEngine.Random.value > 0.5f)
            {
                scale.x *= -1f;
            }

            target.localScale = scale;
        }

        private static void Shuffle(List<SpawnPoint> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                SpawnPoint temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private readonly struct SpawnedPropRecord
        {
            public readonly Vector3 position;
            public readonly float minDistance;

            public SpawnedPropRecord(Vector3 position, float minDistance)
            {
                this.position = position;
                this.minDistance = minDistance;
            }
        }
    }
}
