using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;

public class ResourceUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI valueText;

    private Entity resourceEntity;
    private bool isDirty;

    public void Setup(Entity e)
    {
        resourceEntity = e;
        TickerSystem.OnResourcesProduced += MarkUpdate;
        PurchaseSystem.OnPurchase += MarkUpdate;
        UpdateUI();
    }

    private void OnDestroy()
    {
        TickerSystem.OnResourcesProduced -= MarkUpdate;
        PurchaseSystem.OnPurchase -= MarkUpdate;
    }

    private void MarkUpdate()
    {
        isDirty = true;
    }

    private void Update()
    {
        if (isDirty)
        {
            UpdateUI();
            isDirty = false;
        }
    }

    private void UpdateUI()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var resource = entityManager.GetComponentData<ResourceComponent>(resourceEntity);
        valueText.text = resource.Amount.ToBigNumString();
    }
}
