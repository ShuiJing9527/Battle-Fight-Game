using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler,
    ISubmitHandler
{
    [Header("Scale")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 0.94f;
    [SerializeField] private float clickOvershootScale = 1.1f;

    [Header("Timing")]
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private float exitDuration = 0.15f;
    [SerializeField] private float pressDuration = 0.06f;
    [SerializeField] private float releaseDuration = 0.16f;
    [SerializeField] private float clickActionDelay = 0.18f;

    [Header("Tilt")]
    [SerializeField] private float hoverTiltAngle = 1.5f;
    [SerializeField] private float clickShakeAngle = 2f;
    [SerializeField] private float hoverTiltDuration = 0.14f;
    [SerializeField] private float clickShakeDuration = 0.16f;

    [Header("Color")]
    [SerializeField] private Color hoverTintColor = new Color(1f, 0.949f, 0.722f, 1f);
    [SerializeField, Range(0f, 1f)] private float hoverTintStrength = 0.28f;
    [SerializeField] private float hoverGraphicColorMultiplier = 1.1f;
    [SerializeField] private float pressedGraphicColorMultiplier = 0.88f;
    [SerializeField] private Graphic[] additionalGraphics;

    [Header("Click")]
    [SerializeField] private bool delayButtonOnClick = true;
    [SerializeField] private bool disableButtonDuringDelayedClick = true;

    private RectTransform rectTransform;
    private Button button;
    private UnityEvent delayedClickEvent;
    private readonly List<Graphic> animatedGraphics = new List<Graphic>();
    private readonly Dictionary<Graphic, Color> originalGraphicColors = new Dictionary<Graphic, Color>();
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private bool isPointerInside;
    private bool isSelected;
    private bool isPressed;
    private bool isClickAnimating;
    private bool clickEventsWrapped;
    private Coroutine scaleColorRoutine;
    private Coroutine rotationRoutine;
    private Coroutine delayedClickRoutine;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        button = GetComponent<Button>();
        CaptureOriginalState();
        WrapButtonClickIfNeeded();
    }

    private void OnEnable()
    {
        CaptureOriginalState();
        ApplyStateImmediate(GetRestScaleMultiplier(), GetRestColorMultiplier(), ShouldUseHoverTint());
    }

    private void OnDisable()
    {
        StopAllFeedbackCoroutines();
        isPointerInside = false;
        isSelected = false;
        isPressed = false;
        isClickAnimating = false;
        RestoreOriginalVisuals();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanAnimate())
            return;

        isPointerInside = true;
        AnimateToRestState(hoverDuration, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        if (isSelected || isPressed || isClickAnimating)
            return;

        AnimateToRestState(exitDuration, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanAnimate())
            return;

        isPressed = true;
        StartScaleColorAnimation(pressedScale, pressedGraphicColorMultiplier, false, pressDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        if (isClickAnimating)
            return;

        AnimateToRestState(releaseDuration, false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!CanAnimate())
            return;

        isSelected = true;
        AnimateToRestState(hoverDuration, true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        if (isPointerInside || isPressed || isClickAnimating)
            return;

        AnimateToRestState(exitDuration, false);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!CanAnimate())
            return;

        StartScaleColorAnimation(pressedScale, pressedGraphicColorMultiplier, false, pressDuration);
    }

    private void CaptureOriginalState()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
            originalRotation = rectTransform.localRotation;
        }

        RebuildGraphicCache();
    }

    private void RebuildGraphicCache()
    {
        animatedGraphics.Clear();
        originalGraphicColors.Clear();

        AddGraphic(button != null ? button.targetGraphic : null);
        foreach (TMP_Text tmpText in GetComponentsInChildren<TMP_Text>(true))
            AddGraphic(tmpText);

        foreach (Text text in GetComponentsInChildren<Text>(true))
            AddGraphic(text);

        if (additionalGraphics != null)
        {
            foreach (Graphic graphic in additionalGraphics)
                AddGraphic(graphic);
        }
    }

    private void AddGraphic(Graphic graphic)
    {
        if (graphic == null || animatedGraphics.Contains(graphic))
            return;

        animatedGraphics.Add(graphic);
        originalGraphicColors[graphic] = graphic.color;
    }

    private void WrapButtonClickIfNeeded()
    {
        if (!delayButtonOnClick || clickEventsWrapped || button == null)
            return;

        delayedClickEvent = button.onClick;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(HandleDelayedButtonClick);
        clickEventsWrapped = true;
    }

    private void HandleDelayedButtonClick()
    {
        if (!CanAnimate() || delayedClickEvent == null)
        {
            delayedClickEvent?.Invoke();
            return;
        }

        if (isClickAnimating)
            return;

        delayedClickRoutine = StartCoroutine(PlayClickThenInvoke());
    }

    private IEnumerator PlayClickThenInvoke()
    {
        isClickAnimating = true;
        bool restoreInteractable = false;
        if (disableButtonDuringDelayedClick && button != null && button.interactable)
        {
            restoreInteractable = true;
            button.interactable = false;
        }

        yield return StartCoroutine(PlayClickFeedback());

        UnityEvent clickEvent = delayedClickEvent;
        isClickAnimating = false;
        if (restoreInteractable && button != null)
            button.interactable = true;

        clickEvent?.Invoke();
    }

    private IEnumerator PlayClickFeedback()
    {
        if (scaleColorRoutine != null)
        {
            StopCoroutine(scaleColorRoutine);
            scaleColorRoutine = null;
        }

        float firstStage = Mathf.Min(pressDuration, clickActionDelay * 0.35f);
        float secondStage = Mathf.Max(0.01f, clickActionDelay - firstStage);
        StartRotationSequence(new[] { 0f, -clickShakeAngle, clickShakeAngle, -clickShakeAngle * 0.5f, 0f }, clickShakeDuration);
        yield return StartCoroutine(AnimateScaleAndColor(pressedScale, pressedGraphicColorMultiplier, false, firstStage));
        yield return StartCoroutine(AnimateScaleAndColor(clickOvershootScale, hoverGraphicColorMultiplier, ShouldUseHoverTint(), secondStage * 0.45f));
        yield return StartCoroutine(AnimateScaleAndColor(GetRestScaleMultiplier(), GetRestColorMultiplier(), ShouldUseHoverTint(), secondStage * 0.55f));
    }

    private void AnimateToRestState(float duration, bool playHoverTilt)
    {
        StartScaleColorAnimation(GetRestScaleMultiplier(), GetRestColorMultiplier(), ShouldUseHoverTint(), duration);
        if (playHoverTilt)
            StartRotationSequence(new[] { 0f, -hoverTiltAngle, hoverTiltAngle, 0f }, hoverTiltDuration);
        else
            StartRotationSequence(new[] { 0f }, duration);
    }

    private void StartScaleColorAnimation(float scaleMultiplier, float colorMultiplier, bool useHoverTint, float duration)
    {
        if (scaleColorRoutine != null)
            StopCoroutine(scaleColorRoutine);

        scaleColorRoutine = StartCoroutine(AnimateScaleAndColor(scaleMultiplier, colorMultiplier, useHoverTint, duration));
    }

    private IEnumerator AnimateScaleAndColor(float scaleMultiplier, float colorMultiplier, bool useHoverTint, float duration)
    {
        Vector3 startScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
        Vector3 targetScale = originalScale * Mathf.Max(0f, scaleMultiplier);
        Color[] startColors = CaptureCurrentColors();
        Color[] targetColors = BuildTargetColors(colorMultiplier, useHoverTint);
        float elapsed = 0f;
        duration = Mathf.Max(0.001f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (rectTransform != null)
                rectTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);

            ApplyInterpolatedColors(startColors, targetColors, t);
            yield return null;
        }

        if (rectTransform != null)
            rectTransform.localScale = targetScale;

        ApplyColors(targetColors);
        scaleColorRoutine = null;
    }

    private void StartRotationSequence(float[] localZAngles, float duration)
    {
        if (rotationRoutine != null)
            StopCoroutine(rotationRoutine);

        rotationRoutine = StartCoroutine(AnimateRotationSequence(localZAngles, duration));
    }

    private IEnumerator AnimateRotationSequence(float[] localZAngles, float duration)
    {
        if (rectTransform == null || localZAngles == null || localZAngles.Length == 0)
            yield break;

        duration = Mathf.Max(0.001f, duration);
        Quaternion startRotation = rectTransform.localRotation;
        int segmentCount = Mathf.Max(1, localZAngles.Length - 1);
        float segmentDuration = duration / segmentCount;

        for (int i = 0; i < localZAngles.Length; i++)
        {
            Quaternion from = i == 0 ? startRotation : originalRotation * Quaternion.Euler(0f, 0f, localZAngles[i - 1]);
            Quaternion to = originalRotation * Quaternion.Euler(0f, 0f, localZAngles[i]);
            float elapsed = 0f;
            while (elapsed < segmentDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / segmentDuration));
                rectTransform.localRotation = Quaternion.LerpUnclamped(from, to, t);
                yield return null;
            }
        }

        rectTransform.localRotation = originalRotation;
        rotationRoutine = null;
    }

    private Color[] CaptureCurrentColors()
    {
        Color[] colors = new Color[animatedGraphics.Count];
        for (int i = 0; i < animatedGraphics.Count; i++)
            colors[i] = animatedGraphics[i] != null ? animatedGraphics[i].color : Color.white;

        return colors;
    }

    private Color[] BuildTargetColors(float multiplier, bool useHoverTint)
    {
        Color[] colors = new Color[animatedGraphics.Count];
        for (int i = 0; i < animatedGraphics.Count; i++)
        {
            Graphic graphic = animatedGraphics[i];
            if (graphic == null || !originalGraphicColors.TryGetValue(graphic, out Color originalColor))
            {
                colors[i] = Color.white;
                continue;
            }

            Color color = originalColor;
            if (useHoverTint)
                color = Color.Lerp(color, hoverTintColor, hoverTintStrength);

            color.r = Mathf.Clamp01(color.r * multiplier);
            color.g = Mathf.Clamp01(color.g * multiplier);
            color.b = Mathf.Clamp01(color.b * multiplier);
            color.a = originalColor.a;
            colors[i] = color;
        }

        return colors;
    }

    private void ApplyInterpolatedColors(Color[] startColors, Color[] targetColors, float t)
    {
        for (int i = 0; i < animatedGraphics.Count; i++)
        {
            if (animatedGraphics[i] == null)
                continue;

            animatedGraphics[i].color = Color.Lerp(startColors[i], targetColors[i], t);
        }
    }

    private void ApplyColors(Color[] colors)
    {
        for (int i = 0; i < animatedGraphics.Count; i++)
        {
            if (animatedGraphics[i] == null)
                continue;

            animatedGraphics[i].color = colors[i];
        }
    }

    private void ApplyStateImmediate(float scaleMultiplier, float colorMultiplier, bool useHoverTint)
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale * Mathf.Max(0f, scaleMultiplier);
            rectTransform.localRotation = originalRotation;
        }

        ApplyColors(BuildTargetColors(colorMultiplier, useHoverTint));
    }

    private void RestoreOriginalVisuals()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale * Mathf.Max(0f, normalScale);
            rectTransform.localRotation = originalRotation;
        }

        foreach (KeyValuePair<Graphic, Color> pair in originalGraphicColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }
    }

    private void StopAllFeedbackCoroutines()
    {
        if (scaleColorRoutine != null)
            StopCoroutine(scaleColorRoutine);
        if (rotationRoutine != null)
            StopCoroutine(rotationRoutine);
        if (delayedClickRoutine != null)
            StopCoroutine(delayedClickRoutine);

        scaleColorRoutine = null;
        rotationRoutine = null;
        delayedClickRoutine = null;
    }

    private bool CanAnimate()
    {
        return isActiveAndEnabled && button != null && button.interactable;
    }

    private float GetRestScaleMultiplier()
    {
        return isPointerInside || isSelected ? hoverScale : normalScale;
    }

    private float GetRestColorMultiplier()
    {
        return isPointerInside || isSelected ? hoverGraphicColorMultiplier : 1f;
    }

    private bool ShouldUseHoverTint()
    {
        return isPointerInside || isSelected;
    }
}
