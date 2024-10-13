using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public partial struct TickerSystem : ISystem
{
    private EntityQuery producerQuery, tickerQuery, resourceQuery;
    private ComponentLookup<ResourceComponent> readonlyResourceLookup, writableResourceLookup;

    public static event Action<ResourceComponent[], DescriptionComponent[]> OnResourcesProduced;

    private const int MAX_PRODUCTION_CYCLES = 500;

    public void OnCreate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        producerQuery = entityManager.CreateEntityQuery(typeof(ResourceProducerComponent), typeof(ResourceComponent));
        tickerQuery = entityManager.CreateEntityQuery(typeof(TickerComponent));
        resourceQuery = entityManager.CreateEntityQuery(typeof(ResourceComponent));
        readonlyResourceLookup = state.GetComponentLookup<ResourceComponent>(true);
        writableResourceLookup = state.GetComponentLookup<ResourceComponent>(false);
        Debug.Log("TickerSystem created");
    }

    public void OnUpdate(ref SystemState state)
    {
        var tickerList = tickerQuery.ToEntityArray(Allocator.Temp);
        var ticker = tickerList.FirstOrDefault();
        if (ticker == Entity.Null)
        {
            tickerList.Dispose();
            return;
        }
        var entityManager = state.EntityManager;
        var tickerComponent = entityManager.GetComponentData<TickerComponent>(ticker);

        var currentTime = DateTimeOffset.Now.Ticks;
        readonlyResourceLookup.Update(ref state);
        writableResourceLookup.Update(ref state);

        long productionCycles = math.min((currentTime - tickerComponent.LastTick) / tickerComponent.TickInterval, MAX_PRODUCTION_CYCLES);
        Debug.Log("Production cycles: " + productionCycles);

        if(productionCycles <= 0)
        {
            tickerList.Dispose();
            return;
        }
        var producers = producerQuery.ToEntityArray(Allocator.TempJob);
        var results = new NativeArray<double2>(producers.Length, Allocator.TempJob);

        for (long i = 0; i < productionCycles; i++)
        {
            var productionJob = new ResourceProductionJob
            {
                ResourceLookup = readonlyResourceLookup,
                Results = results
            };

            JobHandle productionJobHandle = productionJob.ScheduleParallel(producerQuery, default);

            var productionApplicationJob = new ProductionApplicationJob
            {
                ResourceLookup = writableResourceLookup,
                Results = results
            };

            JobHandle productionApplicationJobHandle = productionApplicationJob.ScheduleParallel(producerQuery, productionJobHandle);

            var updateLastProductionTimeJob = new UpdateLastProductionTimeJob();

            JobHandle updateLastProductionTimeJobHandle = updateLastProductionTimeJob.ScheduleParallel(tickerQuery, productionApplicationJobHandle);

            updateLastProductionTimeJobHandle.Complete();
        }

        var allResourceEntities = resourceQuery.ToEntityArray(Allocator.Temp);
        var filteredResourceEntities = allResourceEntities.Where(x => entityManager.GetComponentData<ResourceComponent>(x).IsDirty);

        var producedResources = filteredResourceEntities.Select(x => entityManager.GetComponentData<ResourceComponent>(x)).ToArray();
        var descriptions = filteredResourceEntities.Select(x => entityManager.GetComponentData<DescriptionComponent>(x)).ToArray();
        
        OnResourcesProduced?.Invoke(producedResources, descriptions);
        
        allResourceEntities.Dispose();

        var undirty = new UndirtyJob();
        undirty.ScheduleParallel(resourceQuery, default).Complete();

        tickerList.Dispose();
        producers.Dispose();
        results.Dispose();
    }
}
