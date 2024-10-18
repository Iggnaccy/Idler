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
    public Entity baseResourceEntity, tickerEntity, upgradesSaveableEntity;
    public List<Entity> resourceProductionEntities = new List<Entity>();

    private EntityArchetype resourceArchetype, resourceProductionArchetype, upgradeArchetype;
    private EntityArchetype tickerArchetype;
    private SystemHandle tickerSystem;
    private ComponentSystemBase saveLoadSystem;

    public readonly static Version Version = new Version(0, 1, 0);
    public readonly static Version LastSupportedSaveVersion = new Version(0, 1, 0); // Update this when the save format changes

    private static int nextID = 0;
    public static int NextId()
    {
        if (nextID >= 0b11111)
        {
            Debug.LogError("Ran out of IDs!");
            return -1;
        }
        return nextID++;
    }
    private static int upgradeId = 0;
    public static int NextUpgradeId()
    {
        return upgradeId++;
    }

    private void Start()
    {
        gameWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = gameWorld.EntityManager;

        resourceArchetype = entityManager.CreateArchetype(typeof(ResourceComponent), typeof(DescriptionComponent), typeof(SaveableComponent));
        resourceProductionArchetype = entityManager.CreateArchetype(typeof(ResourceProducerComponent), typeof(ResourceComponent), typeof(DescriptionComponent), typeof(SaveableComponent), typeof(PurchasableComponent));
        tickerArchetype = entityManager.CreateArchetype(typeof(TickerComponent), typeof(SaveableComponent)); // No description because it's not a player-visible entity
        upgradeArchetype = entityManager.CreateArchetype(typeof(UpgradeComponent), typeof(DescriptionComponent), typeof(UnlockableComponent), typeof(PurchasableComponent));

        tickerEntity = entityManager.CreateEntity(tickerArchetype);
        entityManager.SetComponentData(tickerEntity, new TickerComponent { LastTick = DateTimeOffset.Now.Ticks, TickInterval = TimeSpan.FromSeconds(interval).Ticks });
        entityManager.SetComponentData(tickerEntity, new SaveableComponent { ID = NextId(), Type = SaveableComponent.SaveableType.Ticker });


        tickerSystem = gameWorld.GetOrCreateSystem<TickerSystem>();
        saveLoadSystem = gameWorld.GetOrCreateSystemManaged<SaveLoadSystem>();

        // Setup Major Layer 1:
        baseResourceEntity = entityManager.CreateEntity(resourceArchetype);
        entityManager.SetComponentData(baseResourceEntity, new ResourceComponent { Amount = new double2(100, 0), IsDirty = true });
        entityManager.SetComponentData(baseResourceEntity, new DescriptionComponent("Base Resource"));
        entityManager.SetComponentData(baseResourceEntity, new SaveableComponent { ID = NextId(), Type = SaveableComponent.SaveableType.Resource });

        // Producers:
        for (int i = 0; i < 10; i++)
        {
            var entity = entityManager.CreateEntity(resourceProductionArchetype);
            entityManager.SetComponentData(entity, new ResourceComponent { Amount = new double2(1, 0), IsDirty = true });
            entityManager.SetComponentData(entity, new ResourceProducerComponent
            {
                ProducedAmount = Double2BigNumExtensions.BigNum.GetNormalized(1, 0),
                ProducedResource = i == 0 ? baseResourceEntity : resourceProductionEntities[i - 1]
            });
            entityManager.SetComponentData(entity, new SaveableComponent { ID = NextId(), Type = SaveableComponent.SaveableType.ResourceProducer }); // ID 2 is the first resource producer (after the base resource and the ticker)
            entityManager.SetComponentData(entity, new DescriptionComponent($"Resource Producer {i}"));
            entityManager.SetComponentData(entity, new PurchasableComponent { NextCostAmount = new double2(1, i + 2), CostCurrency = baseResourceEntity, CostMultiplier = new double2(1.1, 0), NextCostBarrier = new double2(1, 100) });
            resourceProductionEntities.Add(entity);
        }

        // Upgrades:
        upgradesSaveableEntity = entityManager.CreateEntity(typeof(SaveableComponent));
        entityManager.SetComponentData(upgradesSaveableEntity, new SaveableComponent { ID = NextId(), Type = SaveableComponent.SaveableType.Upgrade }); // Singular SaveableComponent for all upgrades - they're being saved in a bitTable

        // Production add upgrades:
        for (int i = 0; i < 10; i++)
        {
            var entity = entityManager.CreateEntity(upgradeArchetype);
            entityManager.SetComponentData(entity, new UpgradeComponent
            {
                Target = resourceProductionEntities[i],
                Modifier = new double2(3, 0),
                IsBought = false,
                Type = UpgradeComponent.UpgradeType.Production,
                SubType = UpgradeComponent.UpgradeSubType.Add,
                UpgradeId = NextUpgradeId()
            });
            entityManager.SetComponentData(entity, new DescriptionComponent($"Increases Resource Producer's {i} production per unit by 3"));
            entityManager.SetComponentData(entity, new UnlockableComponent { Target = resourceProductionEntities[i], AmountToReach = new double2(1, 2), IsUnlocked = false });
            entityManager.SetComponentData(entity, new PurchasableComponent { NextCostAmount = new double2(1, 5 * (i + 1)), CostCurrency = baseResourceEntity, CostMultiplier = double2.zero, NextCostBarrier = double2.zero });
        }

        // Production multiply upgrades:
        for (int i = 0; i < 10; i++)
        {
            var entity = entityManager.CreateEntity(upgradeArchetype);
            entityManager.SetComponentData(entity, new UpgradeComponent
            {
                Target = resourceProductionEntities[i],
                Modifier = new double2(2, 0),
                IsBought = false,
                Type = UpgradeComponent.UpgradeType.Production,
                SubType = UpgradeComponent.UpgradeSubType.Multiply,
                UpgradeId = NextUpgradeId()
            });
            entityManager.SetComponentData(entity, new DescriptionComponent($"Multiplies Resource Producer's {i} production per unit by 2"));
            entityManager.SetComponentData(entity, new UnlockableComponent { Target = resourceProductionEntities[i], AmountToReach = new double2(1, 5), IsUnlocked = false });
            entityManager.SetComponentData(entity, new PurchasableComponent { NextCostAmount = new double2(1, 7 * (i + 1)), CostCurrency = baseResourceEntity, CostMultiplier = double2.zero, NextCostBarrier = double2.zero });
        }

        // Cost div upgrades:
        for (int i = 0; i < 10; i++)
        {
            var entity = entityManager.CreateEntity(upgradeArchetype);
            entityManager.SetComponentData(entity, new UpgradeComponent
            {
                Target = resourceProductionEntities[i],
                Modifier = new double2(0.5, 0),
                IsBought = false,
                Type = UpgradeComponent.UpgradeType.Cost,
                SubType = UpgradeComponent.UpgradeSubType.Divide,
                UpgradeId = NextUpgradeId()
            });
            entityManager.SetComponentData(entity, new DescriptionComponent($"Divides Resource Producer's {i} cost per unit by 2"));
            entityManager.SetComponentData(entity, new UnlockableComponent { Target = resourceProductionEntities[i], AmountToReach = new double2(1, 5), IsUnlocked = false });
            entityManager.SetComponentData(entity, new PurchasableComponent { NextCostAmount = new double2(1, 10 * (i + 1)), CostCurrency = baseResourceEntity, CostMultiplier = double2.zero, NextCostBarrier = double2.zero });
        }

        // Cost sub upgrades:
        // These are part of Major Layer 2, permanent upgrades between rebirths/ascensions/layer 1 resets. They will modify the base cost, rather than current.
        // Therefore, not yet implemented.
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
