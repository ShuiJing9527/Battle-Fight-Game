using UnityEngine;

public class EyeFireTrailIdleMotion : MonoBehaviour
{
    [SerializeField] private Vector3 baseLocalOffset;
    [SerializeField] private bool useCurrentLocalPositionAsBase = true;
    [SerializeField] private float amplitudeX = 0.04f;
    [SerializeField] private float amplitudeY = 0.02f;
    [SerializeField] private float speed = 12f;

    private void Awake()
    {
        if (useCurrentLocalPositionAsBase)
        {
            baseLocalOffset = transform.localPosition;
        }
    }

    private void LateUpdate()
    {
        float x = Mathf.Sin(Time.time * speed) * amplitudeX;
        float y = Mathf.Cos(Time.time * speed * 0.7f) * amplitudeY;
        transform.localPosition = baseLocalOffset + new Vector3(x, y, 0f);
    }
}
