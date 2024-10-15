using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [SerializeField] private double interval = 1.0;

    private World gameWorld;

    private EntityManager entityManager;
    public Entity baseResourceEntity, tickerEntity;
    public List<Entity> resourceProductionEntities = new List<Entity>();

    private EntityArchetype resourceArchetype, resourceProductionArchetype;
    private EntityArchetype tickerArchetype;
    private SystemHandle tickerSystem;
    private ComponentSystemBase saveLoadSystem;

    private void Start()
    {
        gameWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = gameWorld.EntityManager;

        resourceArchetype = entityManager.CreateArchetype(typeof(ResourceComponent), typeof(DescriptionComponent), typeof(SaveableComponent));
        resourceProductionArchetype = entityManager.CreateArchetype(typeof(ResourceProducerComponent), typeof(ResourceComponent), typeof(DescriptionComponent), typeof(SaveableComponent));
        tickerArchetype = entityManager.CreateArchetype(typeof(TickerComponent), typeof(SaveableComponent)); // No description because it's not a player-visible entity

        baseResourceEntity = entityManager.CreateEntity(resourceArchetype);
        entityManager.SetComponentData(baseResourceEntity, new ResourceComponent { Amount = new double2(100, 0), IsDirty = true });
        entityManager.SetComponentData(baseResourceEntity, new DescriptionComponent("Base Resource"));
        entityManager.SetComponentData(baseResourceEntity, new SaveableComponent { ID = 1, Type = SaveableComponent.SaveableType.Resource });

        tickerEntity = entityManager.CreateEntity(tickerArchetype);
        entityManager.SetComponentData(tickerEntity, new TickerComponent { LastTick = DateTimeOffset.Now.Ticks, TickInterval = TimeSpan.FromSeconds(interval).Ticks });
        entityManager.SetComponentData(tickerEntity, new SaveableComponent { ID = 0, Type = SaveableComponent.SaveableType.Ticker });

        tickerSystem = gameWorld.GetOrCreateSystem<TickerSystem>();
        saveLoadSystem = gameWorld.GetOrCreateSystemManaged<SaveLoadSystem>();

        for (int i = 0; i < 10; i++)
        {
            var entity = entityManager.CreateEntity(resourceProductionArchetype);
            entityManager.SetComponentData(entity, new ResourceComponent { Amount = new double2(1, 0), IsDirty = true });
            entityManager.SetComponentData(entity, new ResourceProducerComponent
            {
                ProducedAmount = Double2BigNumExtensions.BigNum.GetNormalized(1, 0),
                ProducedResource = i == 0 ? baseResourceEntity : resourceProductionEntities[i - 1]
            });
            entityManager.SetComponentData(entity, new SaveableComponent { ID = i + 2, Type = SaveableComponent.SaveableType.ResourceProducer }); // ID 2 is the first resource producer (after the base resource and the ticker)
            entityManager.SetComponentData(entity, new DescriptionComponent($"Resource Producer {i}"));
            resourceProductionEntities.Add(entity);
        }
    }

    public void Save()
    {
        gameWorld.EntityManager.CreateSingleton<SaveEventComponent>();
    }

    public void Load()
    {
        gameWorld.EntityManager.CreateSingleton<LoadEventComponent>();
    }

    public EntityManager GetEntityManager()
    {
        return gameWorld.EntityManager;
    }
}
