using UnityEngine;

public class TwinShiftVfxPlayer : MonoBehaviour
{
    [Header("VFX Prefabs")]
    [SerializeField] private GameObject basicSwitchVfxPrefab;
    [SerializeField] private GameObject radianceToTwilightRewardVfxPrefab;
    [SerializeField] private GameObject twilightToRadianceRewardVfxPrefab;

    [Header("Position")]
    [SerializeField] private Vector3 vfxPositionOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float basicSwitchVfxLifetime = 1.5f;
    [SerializeField, Min(0f)] private float rewardVfxLifetime = 2.0f;

    [Header("Debug")]
    [SerializeField] private bool debugTwinShiftVfx;

    private bool warnedMissingBasicSwitchPrefab;
    private bool warnedMissingRadianceToTwilightPrefab;
    private bool warnedMissingTwilightToRadiancePrefab;

    public void PlayBasicSwitchVfx(Vector3 position)
    {
        SpawnPrefabVfx(
            basicSwitchVfxPrefab,
            position,
            basicSwitchVfxLifetime,
            "basicSwitchVfxPrefab",
            ref warnedMissingBasicSwitchPrefab);
    }

    public void PlayRadianceToTwilightRewardVfx(Vector3 position)
    {
        SpawnPrefabVfx(
            radianceToTwilightRewardVfxPrefab,
            position,
            rewardVfxLifetime,
            "radianceToTwilightRewardVfxPrefab",
            ref warnedMissingRadianceToTwilightPrefab);
    }

    public void PlayTwilightToRadianceRewardVfx(Vector3 position)
    {
        SpawnPrefabVfx(
            twilightToRadianceRewardVfxPrefab,
            position,
            rewardVfxLifetime,
            "twilightToRadianceRewardVfxPrefab",
            ref warnedMissingTwilightToRadiancePrefab);
    }

    private void SpawnPrefabVfx(GameObject prefab, Vector3 position, float lifetime, string prefabFieldName, ref bool warnedMissingPrefab)
    {
        if (prefab == null)
        {
            WarnMissingPrefabOnce(prefabFieldName, ref warnedMissingPrefab);
            return;
        }

        Vector3 spawnPosition = position + vfxPositionOffset;
        GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity);
        if (lifetime > 0f)
        {
            Destroy(instance, lifetime);
        }

        DebugVfx($"spawned {prefabFieldName} position={spawnPosition} lifetime={lifetime:F2}");
    }

    private void WarnMissingPrefabOnce(string prefabFieldName, ref bool warnedMissingPrefab)
    {
        if (!debugTwinShiftVfx || warnedMissingPrefab)
        {
            return;
        }

        warnedMissingPrefab = true;
        Debug.Log($"[TwinShiftVfx] {prefabFieldName} is null, skip.", this);
    }

    private void DebugVfx(string message)
    {
        if (!debugTwinShiftVfx)
        {
            return;
        }

        Debug.Log($"[TwinShiftVfx] {message}", this);
    }
}
