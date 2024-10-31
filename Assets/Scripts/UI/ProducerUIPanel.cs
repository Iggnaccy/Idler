using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;

public class ProducerUIPanel : MonoBehaviour
{
    [SerializeField] private ProducerUI uiPrefab;
    [SerializeField] private Transform uiParent;

    NativeArray<Entity> producerEntities;
    private List<ProducerUI> producerUIs = new List<ProducerUI>();

    private bool isDirty = false;

    public void Setup()
    {
        // get all producer entities
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(ResourceProducerComponent));

        producerEntities = query.ToEntityArray(Allocator.Persistent);

        // create UI for each producer
        foreach (var entity in producerEntities)
        {
            var ui = Instantiate(uiPrefab, uiParent);
            ui.Setup(entity);
            producerUIs.Add(ui);
        }

        query.Dispose();

        TickerSystem.OnResourcesProduced += MarkProducerUIsUpdate;
        PurchaseSystem.OnPurchase += MarkProducerUIsUpdate;
    }

    private void OnDestroy()
    {
        TickerSystem.OnResourcesProduced -= MarkProducerUIsUpdate;
        PurchaseSystem.OnPurchase -= MarkProducerUIsUpdate;
        producerEntities.Dispose();
    }

    private void MarkProducerUIsUpdate()
    {
        isDirty = true;
    }

    private void Update()
    {
        if(isDirty)
        {
            foreach (var ui in producerUIs)
            {
                ui.UpdateUI();
            }
            isDirty = false;
        }
    }
}
