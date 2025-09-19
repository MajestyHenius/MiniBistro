using UnityEngine;
using UnityEngine.EventSystems;

public class TestClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[TestClick] �����: {gameObject.name}");
        GetComponent<UnityEngine.UI.Image>().color = Color.red;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"[TestClick] ������: {gameObject.name}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"[TestClick] ����뿪: {gameObject.name}");
    }
}