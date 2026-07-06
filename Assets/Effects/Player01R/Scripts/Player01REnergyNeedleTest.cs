using UnityEngine;

public class Player01REnergyNeedleTest : MonoBehaviour
{
    [Header("Test Setup")]
    [SerializeField] private Player01REnergyNeedle needlePrefab;
    [SerializeField] private Transform startTransform;
    [SerializeField] private Transform targetTransform;

    [Header("Flight")]
    [SerializeField, Min(0.01f)] private float travelSpeed = 48f;
    [SerializeField, Min(0f)] private float passThroughDistance = 5f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;

    [Header("Input")]
    [SerializeField] private KeyCode testKey = KeyCode.T;

    private Player01REnergyNeedle activeNeedle;

    private void Update()
    {
        if (!Input.GetKeyDown(testKey))
        {
            return;
        }

        FireTestNeedle();
    }

    [ContextMenu("Fire Test Needle")]
    public void FireTestNeedle()
    {
        if (needlePrefab == null || startTransform == null || targetTransform == null)
        {
            Debug.LogWarning("[Player01 R Needle Test] Missing prefab or transforms.", this);
            return;
        }

        if (activeNeedle != null)
        {
            Destroy(activeNeedle.gameObject);
        }

        activeNeedle = Instantiate(needlePrefab, startTransform.position, startTransform.rotation);
        activeNeedle.name = needlePrefab.name + "_TestInstance";
        activeNeedle.Launch(
            startTransform.position,
            targetTransform.position,
            travelSpeed,
            passThroughDistance,
            fadeDuration);
    }
}
