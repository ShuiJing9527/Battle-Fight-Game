using UnityEngine;
using System.Reflection;

public class HealthAudioWatcher : MonoBehaviour
{
    [Header("血量脚本，不填就自动找")]
    public MonoBehaviour healthScript;

    [Header("血量变量名")]
    public string healthFieldName = "currentHP";

    [Header("受击音效")]
    public AudioClip hitSfx;

    [Header("死亡音效，可选")]
    public AudioClip deathSfx;

    [Header("音量")]
    [Range(0f, 1f)] public float volume = 1f;

    private int lastHealth;
    private bool initialized = false;
    private bool dead = false;

    private void Start()
    {
        if (healthScript == null)
        {
            FindHealthScriptAutomatically();
        }

        if (healthScript != null)
        {
            lastHealth = GetHealthValue();
            initialized = true;
        }
        else
        {
            Debug.LogWarning(gameObject.name + " 没找到血量脚本，HealthAudioWatcher 不会生效");
        }
    }

    private void Update()
    {
        if (!initialized || healthScript == null) return;

        int currentHealth = GetHealthValue();

        if (currentHealth < lastHealth)
        {
            if (currentHealth <= 0 && !dead)
            {
                dead = true;
                PlayDeathOrHitSound();
            }
            else if (!dead)
            {
                PlayHitSound();
            }
        }

        lastHealth = currentHealth;
    }

    private void FindHealthScriptAutomatically()
    {
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour script in scripts)
        {
            if (script == this) continue;

            if (HasHealthField(script))
            {
                healthScript = script;
                return;
            }
        }
    }

    private bool HasHealthField(MonoBehaviour script)
    {
        if (script == null) return false;

        FieldInfo field = script.GetType().GetField(
            healthFieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        );

        return field != null && field.FieldType == typeof(int);
    }

    private int GetHealthValue()
    {
        if (healthScript == null) return lastHealth;

        FieldInfo field = healthScript.GetType().GetField(
            healthFieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (field == null)
        {
            Debug.LogWarning(gameObject.name + " 找不到血量变量：" + healthFieldName);
            return lastHealth;
        }

        object value = field.GetValue(healthScript);

        if (value is int intValue)
        {
            return intValue;
        }

        Debug.LogWarning(gameObject.name + " 的血量变量不是 int 类型：" + healthFieldName);
        return lastHealth;
    }

    private void PlayHitSound()
    {
        if (hitSfx != null)
        {
            AudioManager.Instance?.PlaySFX(hitSfx, volume);
        }
    }

    private void PlayDeathOrHitSound()
    {
        if (deathSfx != null)
        {
            AudioManager.Instance?.PlaySFX(deathSfx, volume);
        }
        else if (hitSfx != null)
        {
            AudioManager.Instance?.PlaySFX(hitSfx, volume);
        }
    }
}