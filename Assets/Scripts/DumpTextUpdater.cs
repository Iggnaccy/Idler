using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Collections;
using UnityEngine;

public class DumpTextUpdater : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    private Dictionary<string, int> lineOrder = new Dictionary<string, int>();
    private List<string> lines = new List<string>();

    private void Start()
    {
        TickerSystem.OnResourcesProduced += UpdateText;
    }

    private void OnDestroy()
    {
        TickerSystem.OnResourcesProduced -= UpdateText;
    }

    private void UpdateText(ResourceComponent[] resources, DescriptionComponent[] descriptions)
    {
        UpdateDictionary(descriptions);
        UpdateLines(resources, descriptions);
    }

    private void UpdateLines(in ResourceComponent[] resources, in DescriptionComponent[] descriptions)
    {
        for (int i = 0; i < descriptions.Length; i++)
        {
            var description = descriptions[i];
            var resource = resources[i];
            var keyString = GetDescriptionString(description);
            var lineIndex = lineOrder[keyString];
            if(lineIndex >= lines.Count)
            {
                lines.Add($"{keyString}: {resource.Amount.ToBigNumString()}");
            }
            else
            {
                lines[lineIndex] = $"{keyString}: {resource.Amount.ToBigNumString()}";
            }
        }
        text.SetText(string.Join("\n", lines));
    }

    private void UpdateDictionary(in IEnumerable<DescriptionComponent> descriptions)
    {
        foreach (var description in descriptions)
        {
            var count = lineOrder.Count;
            var keyString = GetDescriptionString(description);
            if (!lineOrder.ContainsKey(keyString))
            {
                lineOrder.Add(keyString, count);
            }
        }
    }

    private string GetDescriptionString(in DescriptionComponent description)
    {
        var sb = new StringBuilder();
        foreach (var c in description.Description)
        {
            sb.Append(c);
        }
        return sb.ToString();
    }
}
