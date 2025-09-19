using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LineChart : MonoBehaviour
{
    public RectTransform chartContainer;
    public GameObject linePrefab;
    public GameObject pointPrefab;
    public GameObject labelPrefab;

    private List<GameObject> chartElements = new List<GameObject>();

    public void SetData(List<int> days, List<float> ratings, List<int> incomes)
    {
        // 清除旧图表
        ClearChart();

        // 创建评分折线
        CreateLine(days, ratings, Color.green, "评分");

        // 创建收入折线（需要标准化到与评分相同的范围）
        List<float> normalizedIncomes = NormalizeData(incomes, 0, 10); // 将收入标准化到0-10范围
        CreateLine(days, normalizedIncomes, Color.blue, "收入");

        // 添加标签
        AddLabels(days, ratings, incomes);
    }

    private List<float> NormalizeData(List<int> data, float min, float max)
    {
        List<float> normalized = new List<float>();
        if (data.Count == 0) return normalized;

        int dataMin = data[0];
        int dataMax = data[0];
        foreach (int value in data)
        {
            if (value < dataMin) dataMin = value;
            if (value > dataMax) dataMax = value;
        }

        float range = dataMax - dataMin;
        if (range == 0) range = 1; // 避免除以零

        foreach (int value in data)
        {
            float normalizedValue = min + (value - dataMin) / range * (max - min);
            normalized.Add(normalizedValue);
        }

        return normalized;
    }

    private void CreateLine(List<int> days, List<float> values, Color color, string label)
    {
        if (days.Count != values.Count || days.Count == 0) return;

        GameObject line = Instantiate(linePrefab, chartContainer);
        LineRenderer lineRenderer = line.GetComponent<LineRenderer>();
        if (lineRenderer == null) return;

        // 设置线条颜色
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        // 计算点位置
        Vector3[] positions = new Vector3[days.Count];
        float width = chartContainer.rect.width;
        float height = chartContainer.rect.height;

        for (int i = 0; i < days.Count; i++)
        {
            float x = (float)i / (days.Count - 1) * width;
            float y = values[i] / 10f * height; // 假设最大值是10
            positions[i] = new Vector3(x, y, 0);
        }

        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);

        // 添加数据点
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject point = Instantiate(pointPrefab, chartContainer);
            point.transform.localPosition = positions[i];
            Image pointImage = point.GetComponent<Image>();
            if (pointImage != null) pointImage.color = color;
        }

        chartElements.Add(line);
    }

    private void AddLabels(List<int> days, List<float> ratings, List<int> incomes)
    {
        // 添加X轴标签（天数）
        for (int i = 0; i < days.Count; i++)
        {
            GameObject label = Instantiate(labelPrefab, chartContainer);
            Text labelText = label.GetComponent<Text>();
            if (labelText != null) labelText.text = $"第{days[i]}天";
            label.transform.localPosition = new Vector3(
                (float)i / (days.Count - 1) * chartContainer.rect.width,
                -20f, 0);
        }

        // 添加Y轴标签（评分）
        for (int i = 0; i <= 10; i += 2)
        {
            GameObject label = Instantiate(labelPrefab, chartContainer);
            Text labelText = label.GetComponent<Text>();
            if (labelText != null) labelText.text = i.ToString();
            label.transform.localPosition = new Vector3(
                -30f,
                (float)i / 10f * chartContainer.rect.height, 0);
        }
    }

    private void ClearChart()
    {
        foreach (GameObject element in chartElements)
        {
            Destroy(element);
        }
        chartElements.Clear();
    }
}