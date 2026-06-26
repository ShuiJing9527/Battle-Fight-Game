using UnityEngine;

[CreateAssetMenu(fileName = "DissolveOnDeathProfile", menuName = "Battle Fight/VFX/Dissolve On Death Profile")]
public class DissolveOnDeathProfile : ScriptableObject
{
    [Header("Dissolve")]
    public Shader dissolveShader;
    [Min(0.05f)] public float dissolveDuration = 1.1f;
    [Min(0.01f)] public float dissolveNoise = 12f;
    public Color edgeColor = new Color(1f, 0.62f, 0.18f, 1f);
    [Range(0.01f, 0.5f)] public float edgeWidth = 0.12f;
    [Min(0f)] public float emissionStrength = 2.8f;
    public AnimationCurve dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Death VFX")]
    public GameObject deathVfxPrefab;
    public Vector3 deathVfxOffset = new Vector3(0f, 0.15f, 0f);
    public bool parentVfxToOwner = false;
    [Min(0.1f)] public float vfxAutoDestroyDelay = 2f;
}
