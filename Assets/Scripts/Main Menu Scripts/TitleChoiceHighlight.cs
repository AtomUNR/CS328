using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Put this on a title menu Button. Its linked tower light appears while the
/// button is selected by keyboard/controller or hovered with the mouse.
/// </summary>
public class TitleChoiceHighlight : MonoBehaviour,
    ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject towerFloorLight;

    private bool isSelected;
    private bool isPointerOver;

    private void Awake()
    {
        SetLight(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        RefreshLight();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        RefreshLight();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        RefreshLight();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        RefreshLight();
    }

    private void RefreshLight()
    {
        SetLight(isSelected || isPointerOver);
    }

    private void SetLight(bool visible)
    {
        if (towerFloorLight != null && towerFloorLight.activeSelf != visible)
            towerFloorLight.SetActive(visible);
    }
}
