using UnityEngine;

[ExecuteAlways]
public class LightFlowerGlow : MonoBehaviour
{
    [Header("发光")]
    [SerializeField] private Light glowLight;
    [SerializeField, Min(0f)] private float baseIntensity = 1.4f;
    [SerializeField, Min(0f)] private float pulseAmount = 0.35f;
    [SerializeField, Min(0f)] private float pulseSpeed = 1.2f;

    private float phaseOffset;

    private void OnEnable()
    {
        if (glowLight == null)
        {
            glowLight = GetComponentInChildren<Light>(true);
        }

        phaseOffset = Mathf.Abs(GetInstanceID() * 0.173f) % (Mathf.PI * 2f);
        UpdateGlow();
    }

    private void Update()
    {
        UpdateGlow();
    }

    private void UpdateGlow()
    {
        if (glowLight == null)
        {
            return;
        }

        float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        float pulse = (Mathf.Sin(time * pulseSpeed * Mathf.PI * 2f + phaseOffset) + 1f) * 0.5f;
        glowLight.intensity = baseIntensity + pulse * pulseAmount;
    }
}
