using UnityEngine;

public class EyeFireHorizontalRotationController : MonoBehaviour
{
    [SerializeField] private bool useKeyboardInput = true;
    [SerializeField] private float horizontalThreshold = 0.1f;
    [SerializeField] private float moveRightZAngle = 90f;
    [SerializeField] private float moveLeftZAngle = -90f;
    [SerializeField] private bool returnToIdleWhenNoHorizontalInput = true;
    [SerializeField] private float rotateLerpSpeed = 20f;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private Quaternion idleRotation;
    private bool hasIdleRotation;
    private bool hasInitialTransform;

    private void Awake()
    {
        CacheInitialTransform();
        RestoreInitialTransform();
        CacheIdleRotation();
    }

    private void OnEnable()
    {
        Reinitialize();
    }

    private void Update()
    {
        if (!useKeyboardInput)
        {
            return;
        }

        if (!hasIdleRotation)
        {
            CacheIdleRotation();
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        Quaternion targetRotation = transform.localRotation;

        if (horizontal > horizontalThreshold)
        {
            targetRotation = idleRotation * Quaternion.Euler(0f, 0f, moveRightZAngle);
        }
        else if (horizontal < -horizontalThreshold)
        {
            targetRotation = idleRotation * Quaternion.Euler(0f, 0f, moveLeftZAngle);
        }
        else if (returnToIdleWhenNoHorizontalInput)
        {
            targetRotation = idleRotation;
        }

        if (rotateLerpSpeed <= 0f)
        {
            transform.localRotation = targetRotation;
            return;
        }

        float t = Mathf.Clamp01(Time.deltaTime * rotateLerpSpeed);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, t);
    }

    private void CacheIdleRotation()
    {
        idleRotation = transform.localRotation;
        hasIdleRotation = true;
    }

    private void CacheInitialTransform()
    {
        if (hasInitialTransform)
        {
            return;
        }

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;
        hasInitialTransform = true;
    }

    private void RestoreInitialTransform()
    {
        if (!hasInitialTransform)
        {
            return;
        }

        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;
    }

    public void Reinitialize()
    {
        CacheInitialTransform();
        RestoreInitialTransform();
        CacheIdleRotation();
    }
}
