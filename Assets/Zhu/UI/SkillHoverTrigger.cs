using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SkillHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action<SkillHoverTrigger> entered;
    public Action<SkillHoverTrigger> exited;
    public Action<SkillHoverTrigger> clicked;

    [NonSerialized] public string skillKey;
    [NonSerialized] public int playerIndex;

    public void OnPointerEnter(PointerEventData eventData)
    {
        entered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        exited?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        clicked?.Invoke(this);
    }
}
