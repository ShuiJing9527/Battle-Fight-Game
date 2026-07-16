using UnityEngine;
using UnityEngine.EventSystems;

public class RuneSlotHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RuneUIController controller;
    private RuneDefinition rune;
    private int skillIndex;
    private int slotIndex;

    public void Configure(RuneUIController owner, int configuredSkillIndex, int configuredSlotIndex, RuneDefinition configuredRune)
    {
        controller = owner;
        skillIndex = configuredSkillIndex;
        slotIndex = configuredSlotIndex;
        rune = configuredRune;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        controller?.HandleRuneSlotHoverEnter(skillIndex, slotIndex, rune, transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        controller?.HandleRuneSlotHoverExit(skillIndex, slotIndex, rune, transform);
    }
}
