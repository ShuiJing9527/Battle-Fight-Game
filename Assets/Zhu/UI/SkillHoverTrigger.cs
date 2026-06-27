using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SkillHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action<SkillHoverTrigger> entered;
    public Action<SkillHoverTrigger> exited;

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
}
