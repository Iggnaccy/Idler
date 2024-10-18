using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class DumpTextUpdater : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private GameController gameController;

    private Dictionary<int, string> displayLines = new Dictionary<int, string>();
    private StringBuilder sb = new StringBuilder();

    private void Start()
    {
        TickerSystem.OnResourcesProduced += UpdateText;
    }

    private void OnDestroy()
    {
        TickerSystem.OnResourcesProduced -= UpdateText;
    }

    private void UpdateText()
    {
        sb.Clear();
        var entityManager = gameController.GetEntityManager();
        var baseResource = entityManager.GetComponentData<ResourceComponent>(gameController.baseResourceEntity);
        if(baseResource.IsDirty)
        {
            var baseDescription = entityManager.GetComponentData<DescriptionComponent>(gameController.baseResourceEntity);
            displayLines[0] = $"{GetDescriptionString(baseDescription)}: {baseResource.Amount.ToBigNumString()}";
        }

        var resources = new ResourceComponent[gameController.resourceProductionEntities.Count];
        for (int i = 0; i < gameController.resourceProductionEntities.Count; i++)
        {
            resources[i] = entityManager.GetComponentData<ResourceComponent>(gameController.resourceProductionEntities[i]);
            if(resources[i].IsDirty)
            {
                var description = entityManager.GetComponentData<DescriptionComponent>(gameController.resourceProductionEntities[i]);
                displayLines[i + 1] = $"{GetDescriptionString(description)}: {resources[i].Amount.ToBigNumString()}";
            }
        }

        foreach (var line in displayLines)
        {
            sb.AppendLine(line.Value);
        }

        text.SetText(sb.ToString());
    }

    //private void UpdateLines(in ResourceComponent[] resources, in DescriptionComponent[] descriptions)
    //{
    //    for (int i = 0; i < descriptions.Length; i++)
    //    {
    //        var description = descriptions[i];
    //        var resource = resources[i];
    //        var keyString = GetDescriptionString(description);
    //        var lineIndex = lineOrder[keyString];
    //        if(lineIndex >= lines.Count)
    //        {
    //            lines.Add($"{keyString}: {resource.Amount.ToBigNumString()}");
    //        }
    //        else
    //        {
    //            lines[lineIndex] = $"{keyString}: {resource.Amount.ToBigNumString()}";
    //        }
    //    }
    //    text.SetText(string.Join("\n", lines));
    //}
    //
    //private void UpdateDictionary(in IEnumerable<DescriptionComponent> descriptions)
    //{
    //    foreach (var description in descriptions)
    //    {
    //        var count = lineOrder.Count;
    //        var keyString = GetDescriptionString(description);
    //        if (!lineOrder.ContainsKey(keyString))
    //        {
    //            lineOrder.Add(keyString, count);
    //        }
    //    }
    //}
    //
    private string GetDescriptionString(in DescriptionComponent description)
    {
        var descSB = new StringBuilder();
        foreach (var c in description.Description)
        {
            descSB.Append(c);
        }
        return descSB.ToString();
    }
}
