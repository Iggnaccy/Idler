using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public partial struct ProductionApplicationJob : IJobEntity
{
    [NativeDisableParallelForRestriction] public ComponentLookup<ResourceComponent> ResourceLookup;
    [ReadOnly] public NativeArray<double2> Results;
    public void Execute([EntityIndexInQuery] int entityIndex, in ResourceProducerComponent resourceProducer)
    {
        var resource = ResourceLookup[resourceProducer.ProducedResource];
        var final = Results[entityIndex];
        final.AddBigNum(resource.Amount);
        resource.Amount = final;
        ResourceLookup[resourceProducer.ProducedResource] = resource; // This causes issues if ever there are two resource producers producing the same resource
    }
}
