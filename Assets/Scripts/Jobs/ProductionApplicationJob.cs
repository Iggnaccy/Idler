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
        var final = Results[entityIndex];

        if(final.x <= 0)
        {
            return; // No resources to produce
        }

        var resource = ResourceLookup[resourceProducer.ProducedResource];
        final.AddBigNum(resource.Amount);
        resource.Amount = final;
        ResourceLookup[resourceProducer.ProducedResource] = resource; 
    }
}
