using UnityEngine;
using Spine.Unity;

public class Player2HaloRotateEffect : MonoBehaviour
{
    [SerializeField] private SkeletonAnimation spineTarget;
    [SerializeField] private bool followSpineFacingOffset = true;
    [SerializeField] private Vector3 leftFacingLocalOffset = new Vector3(0.325f, 4.375f, 3f);
    [SerializeField] private Vector3 rightFacingLocalOffset = new Vector3(-0.325f, 4.375f, 3f);
    [SerializeField] private float rotateSpeed = 25f;
    [SerializeField] private float skillRotateSpeed = 260f;
    [SerializeField] private float skillBoostDuration = 0.65f;
    [SerializeField] private float speedLerpRate = 10f;
    [SerializeField] private bool rotateClockwise = true;
    [SerializeField] private bool unscaledTime = false;

    private float skillBoostTimer;
    private float currentRotateSpeed;

    private void Awake()
    {
        currentRotateSpeed = rotateSpeed;
    }

    private void LateUpdate()
    {
        UpdateFacingOffset();
        UpdateRotation();
    }

    private void UpdateFacingOffset()
    {
        if (!followSpineFacingOffset)
        {
            return;
        }

        if (spineTarget == null || spineTarget.Skeleton == null)
        {
            return;
        }

        transform.localPosition = spineTarget.Skeleton.ScaleX > 0f ? leftFacingLocalOffset : rightFacingLocalOffset;
    }

    private void UpdateRotation()
    {
        float deltaTime = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (skillBoostTimer > 0f)
        {
            skillBoostTimer -= deltaTime;
        }

        float targetSpeed = skillBoostTimer > 0f ? skillRotateSpeed : rotateSpeed;
        currentRotateSpeed = Mathf.Lerp(currentRotateSpeed, targetSpeed, Mathf.Clamp01(speedLerpRate * deltaTime));
        float direction = rotateClockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, direction * currentRotateSpeed * deltaTime, Space.Self);
    }

    public void TriggerSkillBoost()
    {
        skillBoostTimer = Mathf.Max(skillBoostTimer, skillBoostDuration);
    }
}
