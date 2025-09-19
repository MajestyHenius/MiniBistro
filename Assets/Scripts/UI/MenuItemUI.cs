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
        if (priceText != null) priceText.text = $"价格: {item.price}";
        if (attributeText != null) attributeText.text = $"属性: {item.attribute}";
        if (popularityText != null)
        {
            popularityText.text = $"受欢迎度: {item.popularity}";

            // 根据受欢迎度设置颜色
            if (item.popularity >= 80)
                popularityText.color = Color.green;
            else if (item.popularity >= 50)
                popularityText.color = Color.yellow;
            else
                popularityText.color = Color.red;
        }

        // 可以根据受欢迎度设置不同背景色
        if (backgroundImage != null)
        {
            float popularityFactor = Mathf.Clamp01(item.popularity / 100f);
            backgroundImage.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.5f), popularityFactor);
        }
    }
}