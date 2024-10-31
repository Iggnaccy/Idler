using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class ProducerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText, descriptionText, boughtText, productionText, priceText;
    [SerializeField] private Button buyButton;

    private Entity entity;

    public void Setup(Entity e)
    {
        entity = e;
        buyButton.onClick.AddListener(OnBuyButtonUp);

        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var name = entityManager.GetComponentData<NameComponent>(entity);
        nameText.text = name.ToString();

        UpdateUI();
    }

    public void UpdateUI()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var purchasable = entityManager.GetComponentData<PurchasableComponent>(entity);
        var resource = entityManager.GetComponentData<ResourceComponent>(entity);
        var producer = entityManager.GetComponentData<ResourceProducerComponent>(entity);
        var description = entityManager.GetComponentData<DescriptionComponent>(entity);
        
        UpdateDisplay(description, producer, resource, purchasable);
        
        var requiredResource = entityManager.GetComponentData<ResourceComponent>(purchasable.CostCurrency);
        buyButton.interactable = requiredResource.Amount.IsBigNumGreaterOrEqualThan(purchasable.NextCostAmount);
    }

    private void OnBuyButtonUp()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var newEntity = entityManager.CreateEntity(typeof(PurchaseEvent));
        entityManager.SetComponentData(newEntity, new PurchaseEvent {Entity = entity, Type = PurchaseEvent.PurchaseType.Producer});
    }

    private void UpdateDisplay(in DescriptionComponent description, in ResourceProducerComponent producer, in ResourceComponent resource, in PurchasableComponent purchasable)
    {
        descriptionText.text = description.ToString();
        boughtText.text = resource.Amount.ToBigNumString();
        productionText.text = producer.ProducedAmount.MultiplyBigNumR(resource.Amount).ToBigNumString() + " per tick";
        priceText.text = purchasable.NextCostAmount.ToBigNumString();
    }
}
