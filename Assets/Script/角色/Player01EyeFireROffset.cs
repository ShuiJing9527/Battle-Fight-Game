using System.Collections;
using UnityEngine;

public class Player01EyeFireROffset : MonoBehaviour
{
    [SerializeField] private Transform offsetRoot;
    [SerializeField, Min(0f)] private float backwardOffset = 0.8f;
    [SerializeField] private float backwardVerticalOffset = 0f;
    [SerializeField, Min(0f)] private float backwardDuration = 0.08f;
    [SerializeField] private Vector3 rHoldLocalOffset = Vector3.zero;
    [SerializeField] private bool rHoldOffsetIsForFacingRight = true;
    [SerializeField, Min(0f)] private float returnBackwardOffset = 0.15f;
    [SerializeField] private float returnBackwardVerticalOffset = 0f;
    [SerializeField, Min(0f)] private float returnBackwardDuration = 0.10f;
    [SerializeField, Min(0f)] private float forwardOffset = 0.35f;
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField, Min(0f)] private float moveInDuration = 0.10f;
    [SerializeField, Min(0f)] private float holdDuration = 0.08f;
    [SerializeField, Min(0f)] private float returnDuration = 0.08f;
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private bool debugLog = false;

    private Coroutine offsetRoutine;
    private Coroutine returnRoutine;
    private bool offsetActive;
    private float lockedFacingSign = 1f;
    private Vector3 lastWrittenPosition = Vector3.zero;
    private bool wrotePositionThisFrame;
    private bool externalOverwriteLogged;

    public Transform OffsetRoot => offsetRoot;
    public Vector3 DiagnosticRHoldLocalOffset => rHoldLocalOffset;
    public float DiagnosticBackwardOffset => backwardOffset;
    public float DiagnosticBackwardDuration => backwardDuration;
    public float DiagnosticMoveInDuration => moveInDuration;
    public float DiagnosticReturnBackwardOffset => returnBackwardOffset;

    private void OnDisable()
    {
        ImmediateReset();
    }

    private void OnDestroy()
    {
        ImmediateReset();
    }

    public void BeginROffset(float castFacingSign)
    {
        Debug.Log(
            $"[R EyeFire] BeginROffset called\n" +
            $"[R EyeFire] castFacingSign = {castFacingSign:F2}\n" +
            $"[R EyeFire] backwardOffset = {backwardOffset:F2}\n" +
            $"[R EyeFire] backwardDuration = {backwardDuration:F2}\n" +
            $"[R EyeFire] moveInDuration = {moveInDuration:F2}\n" +
            $"[R EyeFire] rHoldLocalOffset = {rHoldLocalOffset}\n" +
            $"[R EyeFire] returnBackwardOffset = {returnBackwardOffset:F2}",
            this);

        if (offsetRoot == null)
        {
            Debug.LogWarning("[R EyeFire] BeginROffset aborted because Offset Root is null.", this);
            return;
        }

        StopOffsetCoroutines();
        ResetOffsetRootToZero();
        offsetActive = true;
        lockedFacingSign = castFacingSign;
        externalOverwriteLogged = false;

        Vector3 backwardTarget = ResolveBackwardTargetPosition(castFacingSign);
        Vector3 rHoldTarget = ResolveRHoldTarget(castFacingSign);
        Debug.Log($"[R EyeFire] Stage = Start, position = {GetCurrentPosition()}", this);
        Debug.Log($"[R EyeFire] Stage = BackwardTarget, target = {backwardTarget}", this);
        Debug.Log($"[R EyeFire] rHoldLocalOffset inspector = {rHoldLocalOffset}", this);
        Debug.Log($"[R EyeFire] rHoldTarget resolved = {rHoldTarget}", this);
        offsetRoutine = StartCoroutine(AnimateOffsetSequence(backwardTarget, rHoldTarget));

        if (debugLog)
        {
            Debug.Log($"[Player01 EyeFire R Offset] Begin backward={backwardTarget} hold={rHoldTarget}", this);
        }
    }

    public void EndROffset()
    {
        if (offsetRoot == null)
        {
            return;
        }

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            ImmediateReset();
            return;
        }

        StopOffsetCoroutines();
        offsetActive = false;
        Debug.Log($"[R EyeFire] Stage = ReturnZero, target = {Vector3.zero}", this);
        returnRoutine = StartCoroutine(ReturnToOrigin(Mathf.Max(0f, returnDuration)));

        if (debugLog)
        {
            Debug.Log($"[Player01 EyeFire R Offset] End requested, castFacingSign={lockedFacingSign:F2}", this);
        }
    }

    private IEnumerator AnimateOffsetSequence(Vector3 backwardTarget, Vector3 rHoldTarget)
    {
        yield return AnimatePhase(backwardTarget, Mathf.Max(0f, backwardDuration));
        if (!offsetActive || offsetRoot == null)
        {
            offsetRoutine = null;
            yield break;
        }

        Debug.Log($"[R EyeFire] Stage = HoldTarget, target = {rHoldTarget}", this);
        yield return AnimatePhase(rHoldTarget, Mathf.Max(0f, moveInDuration));
        if (offsetRoot != null)
        {
            SetCurrentPosition(rHoldTarget);
        }

        while (offsetRoot != null && offsetActive)
        {
            yield return null;
        }

        offsetRoutine = null;
    }

    private IEnumerator AnimatePhase(Vector3 targetPosition, float duration)
    {
        if (offsetRoot == null || !offsetActive)
        {
            yield break;
        }

        Vector3 startPosition = GetCurrentPosition();
        if (duration <= 0f)
        {
            SetCurrentPosition(targetPosition);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && offsetRoot != null && offsetActive)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetCurrentPosition(Vector3.Lerp(startPosition, targetPosition, t));
            yield return null;
        }

        if (offsetRoot != null && offsetActive)
        {
            SetCurrentPosition(targetPosition);
        }
    }

    private IEnumerator ReturnToOrigin(float duration)
    {
        Vector3 origin = Vector3.zero;
        Vector3 startPosition = GetCurrentPosition();
        if (duration <= 0f)
        {
            SetCurrentPosition(origin);
            ClearOffsetState();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && offsetRoot != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetCurrentPosition(Vector3.Lerp(startPosition, origin, t));
            yield return null;
        }

        if (offsetRoot != null)
        {
            SetCurrentPosition(origin);
        }

        ClearOffsetState();
        Debug.Log($"[R EyeFire] Stage = Complete, position = {GetCurrentPosition()}", this);
    }

    private Vector3 ResolveBackwardTargetPosition(float castFacingSign)
    {
        float backwardX;

        if (castFacingSign < 0f)
        {
            backwardX = Mathf.Abs(backwardOffset);
        }
        else
        {
            backwardX = -Mathf.Abs(backwardOffset);
        }

        return new Vector3(backwardX, backwardVerticalOffset, 0f);
    }

    private Vector3 ResolveRHoldTarget(float castFacingSign)
    {
        bool characterFacingRight = castFacingSign > 0f;
        bool useAuthoredX = characterFacingRight == rHoldOffsetIsForFacingRight;

        float resolvedX = useAuthoredX
            ? rHoldLocalOffset.x
            : -rHoldLocalOffset.x;

        return new Vector3(
            resolvedX,
            rHoldLocalOffset.y,
            rHoldLocalOffset.z);
    }

    private Vector3 GetCurrentPosition()
    {
        if (offsetRoot == null)
        {
            return Vector3.zero;
        }

        return offsetRoot.localPosition;
    }

    private void SetCurrentPosition(Vector3 position)
    {
        if (offsetRoot == null)
        {
            return;
        }

        offsetRoot.localPosition = position;
        lastWrittenPosition = position;
        wrotePositionThisFrame = true;
    }

    private void LateUpdate()
    {
        if (offsetRoot == null)
        {
            wrotePositionThisFrame = false;
            return;
        }

        if (!wrotePositionThisFrame &&
            !externalOverwriteLogged &&
            (offsetRoutine != null || returnRoutine != null || offsetActive) &&
            (offsetRoot.localPosition - lastWrittenPosition).sqrMagnitude > 0.000001f)
        {
            externalOverwriteLogged = true;
            Debug.LogWarning(
                $"[R EyeFire] External position overwrite detected\n" +
                $"Expected = {lastWrittenPosition}\n" +
                $"Actual = {offsetRoot.localPosition}",
                this);
        }

        wrotePositionThisFrame = false;
    }

    public void ImmediateReset()
    {
        StopOffsetCoroutines();
        ResetOffsetRootToZero();
        ClearOffsetState();
    }

    private void StopOffsetCoroutines()
    {
        if (offsetRoutine != null)
        {
            StopCoroutine(offsetRoutine);
            offsetRoutine = null;
        }

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }
    }

    private void ClearOffsetState()
    {
        offsetActive = false;
        offsetRoutine = null;
        returnRoutine = null;
    }

    private void ResetOffsetRootToZero()
    {
        if (offsetRoot == null)
        {
            return;
        }

        offsetRoot.localPosition = Vector3.zero;
        lastWrittenPosition = Vector3.zero;
        wrotePositionThisFrame = true;
    }
}
