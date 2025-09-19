using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text attributeText;
    [SerializeField] private TMP_Text popularityText;
    [SerializeField] private Image backgroundImage;

    public void Setup(RestaurantManager.MenuItem item)
    {
        if (nameText != null) nameText.text = item.name;
        if (priceText != null) priceText.text = $"�۸�: {item.price}";
        if (attributeText != null) attributeText.text = $"����: {item.attribute}";
        if (popularityText != null)
        {
            popularityText.text = $"�ܻ�ӭ��: {item.popularity}";

            // �����ܻ�ӭ��������ɫ
            if (item.popularity >= 80)
                popularityText.color = Color.green;
            else if (item.popularity >= 50)
                popularityText.color = Color.yellow;
            else
                popularityText.color = Color.red;
        }

        // ���Ը����ܻ�ӭ�����ò�ͬ����ɫ
        if (backgroundImage != null)
        {
            float popularityFactor = Mathf.Clamp01(item.popularity / 100f);
            backgroundImage.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.5f), popularityFactor);
        }
    }
}