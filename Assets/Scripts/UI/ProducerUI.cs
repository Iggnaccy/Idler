using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class ProducerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI descriptionText, boughtText, productionText, priceText;
    [SerializeField] private Button buyButton;

    private Entity entity;

    public void Setup(Entity e)
    {
        entity = e;
        TickerSystem.OnResourcesProduced += OnResourcesProduced;
    }

    private void OnDestroy()
    {
        TickerSystem.OnResourcesProduced -= OnResourcesProduced;
    }

    private void OnResourcesProduced()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var purchasable = entityManager.GetComponentData<PurchasableComponent>(entity);
        var resource = entityManager.GetComponentData<ResourceComponent>(entity);
        var producer = entityManager.GetComponentData<ResourceProducerComponent>(entity);
        if (resource.IsDirty)
        {
            UpdateUI(producer, resource, purchasable);
        }

        var requiredResource = entityManager.GetComponentData<ResourceComponent>(producer.ProducedResource);
        buyButton.interactable = requiredResource.Amount.IsBigNumGreaterThan(purchasable.NextCostAmount);
    }

    private void OnBuyButtonUp()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var newEntity = entityManager.CreateEntity(typeof(PurchaseEvent));
        entityManager.SetComponentData(newEntity, new PurchaseEvent {Entity = entity});
    }

    private void UpdateUI(in ResourceProducerComponent producer, in ResourceComponent resource, in PurchasableComponent purchasable)
    {
        descriptionText.text = "Produces " + producer.ProducedAmount.ToBigNumString() + " " + producer.ProducedResource;
        boughtText.text = "Bought: " + resource.Amount.ToBigNumString();
        productionText.text = "Production: " + producer.ProducedAmount.ToBigNumString();
        priceText.text = "Price: " + purchasable.NextCostAmount.ToBigNumString();
    }
}
