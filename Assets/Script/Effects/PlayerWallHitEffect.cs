using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerWallHitEffect : MonoBehaviour
{
    private static readonly int HueOffsetId = Shader.PropertyToID("_HueOffset");

    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private float destroyDelay = 1.5f;
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private bool spawnOnStay = false;
    [SerializeField] private int maxActiveEffects = 1;

    private float nextEffectTime;
    private readonly List<GameObject> activeEffects = new List<GameObject>();

    private void OnCollisionEnter(Collision collision)
    {
        TryPlayEffect(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!spawnOnStay)
        {
            return;
        }

        TryPlayEffect(collision);
    }

    private void TryPlayEffect(Collision collision)
    {
        CleanupActiveEffects();

        if (Time.time < nextEffectTime ||
            activeEffects.Count >= Mathf.Max(1, maxActiveEffects) ||
            collision.contactCount == 0 ||
            !IsWall(collision.collider))
        {
            return;
        }

        if (effectPrefab == null)
        {
            Debug.LogWarning("PlayerWallHitEffect has no effect prefab assigned.", this);
            nextEffectTime = Time.time + cooldown;
            return;
        }

        ContactPoint contact = collision.GetContact(0);

        Vector3 normal = contact.normal.sqrMagnitude > 0.001f
            ? contact.normal
            : -transform.forward;

        Vector3 surfaceNormal = normal.normalized;
        Vector3 position = new Vector3(contact.point.x, transform.position.y + heightOffset, contact.point.z) + surfaceNormal * surfaceOffset;
        Quaternion rotation = Quaternion.LookRotation(surfaceNormal, Vector3.up);

        float hueOffset = Random.Range(0f, 1f);

        GameObject effectInstance = Instantiate(effectPrefab, position, rotation);
        ApplyRandomMaterialParameters(effectInstance, hueOffset);
        activeEffects.Add(effectInstance);
        Destroy(effectInstance, destroyDelay);
        StartCoroutine(ReleaseEffectAfterDelay(effectInstance, destroyDelay));

        nextEffectTime = Time.time + cooldown;
    }

    private void ApplyRandomMaterialParameters(GameObject effectInstance, float hueOffset)
    {
        if (effectInstance == null)
        {
            return;
        }

        Renderer[] renderers = effectInstance.GetComponentsInChildren<Renderer>();

        foreach (Renderer effectRenderer in renderers)
        {
            if (effectRenderer == null)
            {
                continue;
            }

            Material[] instanceMaterials = effectRenderer.materials;
            foreach (Material material in instanceMaterials)
            {
                if (material != null && material.HasProperty(HueOffsetId))
                {
                    material.SetFloat(HueOffsetId, hueOffset);
                }
            }
        }
    }

    private bool IsWall(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (wallLayers.value != 0)
        {
            int otherLayer = other.gameObject.layer;
            return (wallLayers.value & (1 << otherLayer)) != 0;
        }

        return IsLikelyWallName(other.transform);
    }

    private bool IsLikelyWallName(Transform target)
    {
        while (target != null)
        {
            string objectName = target.name;
            if (objectName.Contains("Wall") ||
                objectName.Contains("WallTilemap") ||
                objectName.Contains("Merged Wall Colliders"))
            {
                return true;
            }

            target = target.parent;
        }

        return false;
    }

    private IEnumerator ReleaseEffectAfterDelay(GameObject effectInstance, float delay)
    {
        yield return new WaitForSeconds(delay);
        activeEffects.Remove(effectInstance);
        CleanupActiveEffects();
    }

    private void CleanupActiveEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (activeEffects[i] == null)
            {
                activeEffects.RemoveAt(i);
            }
        }
    }
}
