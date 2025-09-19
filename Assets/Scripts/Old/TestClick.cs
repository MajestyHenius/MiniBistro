using UnityEngine;
using UnityEngine.EventSystems;

public class TestClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[TestClick] 点击了: {gameObject.name}");
        GetComponent<UnityEngine.UI.Image>().color = Color.red;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"[TestClick] 鼠标进入: {gameObject.name}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"[TestClick] 鼠标离开: {gameObject.name}");
    }
}