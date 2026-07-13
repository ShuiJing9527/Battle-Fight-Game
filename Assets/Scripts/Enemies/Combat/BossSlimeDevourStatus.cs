using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossSlimeDevourStatus : MonoBehaviour
{
    private readonly List<SpriteColorBinding> spriteBindings = new List<SpriteColorBinding>();
    private readonly List<MaterialColorBinding> materialBindings = new List<MaterialColorBinding>();

    private Coroutine activeRoutine;
    private CombatHealth combatHealth;
    private Rigidbody targetBody;

    private struct SpriteColorBinding
    {
        public SpriteRenderer renderer;
        public Color originalColor;
    }

    private struct MaterialColorBinding
    {
        public Renderer renderer;
        public Material material;
        public string propertyName;
        public Color originalColor;
    }

    public static BossSlimeDevourStatus ResolveOrAdd(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        BossSlimeDevourStatus status = target.GetComponent<BossSlimeDevourStatus>();
        if (status == null)
        {
            status = target.AddComponent<BossSlimeDevourStatus>();
        }

        return status;
    }

    public void Apply(
        GameObject damageSource,
        Transform holdAnchor,
        float duration,
        float tickInterval,
        float damagePerTick,
        Color darkTint,
        Vector3 holdOffset)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            RestoreVisuals();
        }

        CacheRuntimeReferences();
        CacheVisuals();
        ApplyDarkTint(darkTint);
        activeRoutine = StartCoroutine(DevourRoutine(damageSource, holdAnchor, duration, tickInterval, damagePerTick, holdOffset));
    }

    private IEnumerator DevourRoutine(
        GameObject damageSource,
        Transform holdAnchor,
        float duration,
        float tickInterval,
        float damagePerTick,
        Vector3 holdOffset)
    {
        float elapsed = 0f;
        float nextDamageTime = 0f;
        float safeDuration = Mathf.Max(0.1f, duration);
        float safeTickInterval = Mathf.Max(0.05f, tickInterval);

        while (elapsed < safeDuration)
        {
            if (combatHealth == null || combatHealth.IsDead)
            {
                break;
            }

            HoldInsideBossBody(holdAnchor, holdOffset);

            if (elapsed >= nextDamageTime)
            {
                combatHealth.TakeDamage(new BattleDamage(Mathf.Max(0f, damagePerTick), BattleDamageType.Special, damageSource));
                nextDamageTime += safeTickInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreVisuals();
        activeRoutine = null;
    }

    private void CacheRuntimeReferences()
    {
        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
            if (combatHealth == null)
            {
                combatHealth = GetComponentInParent<CombatHealth>();
            }
            if (combatHealth == null)
            {
                combatHealth = GetComponentInChildren<CombatHealth>(true);
            }
        }

        if (targetBody == null)
        {
            targetBody = GetComponent<Rigidbody>();
            if (targetBody == null)
            {
                targetBody = GetComponentInParent<Rigidbody>();
            }
        }
    }

    private void CacheVisuals()
    {
        spriteBindings.Clear();
        materialBindings.Clear();

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteBindings.Add(new SpriteColorBinding
            {
                renderer = spriteRenderer,
                originalColor = spriteRenderer.color
            });
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is SpriteRenderer || renderer.sharedMaterial == null)
            {
                continue;
            }

            Material material = renderer.material;
            string propertyName = ResolveColorPropertyName(material);
            if (string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            materialBindings.Add(new MaterialColorBinding
            {
                renderer = renderer,
                material = material,
                propertyName = propertyName,
                originalColor = material.GetColor(propertyName)
            });
        }
    }

    private void ApplyDarkTint(Color darkTint)
    {
        for (int i = 0; i < spriteBindings.Count; i++)
        {
            SpriteColorBinding binding = spriteBindings[i];
            if (binding.renderer == null)
            {
                continue;
            }

            Color color = binding.originalColor;
            binding.renderer.color = new Color(color.r * darkTint.r, color.g * darkTint.g, color.b * darkTint.b, color.a);
        }

        for (int i = 0; i < materialBindings.Count; i++)
        {
            MaterialColorBinding binding = materialBindings[i];
            if (binding.material == null || string.IsNullOrEmpty(binding.propertyName))
            {
                continue;
            }

            Color color = binding.originalColor;
            binding.material.SetColor(binding.propertyName, new Color(color.r * darkTint.r, color.g * darkTint.g, color.b * darkTint.b, color.a));
        }
    }

    private void HoldInsideBossBody(Transform holdAnchor, Vector3 holdOffset)
    {
        if (holdAnchor == null)
        {
            return;
        }

        Vector3 targetPosition = holdAnchor.position + holdOffset;
        if (targetBody != null)
        {
            targetBody.linearVelocity = Vector3.zero;
            targetBody.position = Vector3.Lerp(targetBody.position, targetPosition, Mathf.Clamp01(Time.deltaTime * 12f));
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, Mathf.Clamp01(Time.deltaTime * 12f));
    }

    private void RestoreVisuals()
    {
        for (int i = 0; i < spriteBindings.Count; i++)
        {
            SpriteColorBinding binding = spriteBindings[i];
            if (binding.renderer != null)
            {
                binding.renderer.color = binding.originalColor;
            }
        }

        for (int i = 0; i < materialBindings.Count; i++)
        {
            MaterialColorBinding binding = materialBindings[i];
            if (binding.material != null && !string.IsNullOrEmpty(binding.propertyName))
            {
                binding.material.SetColor(binding.propertyName, binding.originalColor);
            }
        }

        spriteBindings.Clear();
        materialBindings.Clear();
    }

    private void OnDisable()
    {
        RestoreVisuals();
    }

    private void OnDestroy()
    {
        RestoreVisuals();
    }

    private static string ResolveColorPropertyName(Material material)
    {
        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_BaseColor"))
        {
            return "_BaseColor";
        }

        if (material.HasProperty("_Color"))
        {
            return "_Color";
        }

        return null;
    }
}
