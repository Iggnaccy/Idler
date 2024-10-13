using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct ResourceProductionJob : IJobEntity
{
    [NativeDisableParallelForRestriction, ReadOnly] public ComponentLookup<ResourceComponent> ResourceLookup;
    public NativeArray<double2> Results;

    private void Execute([EntityIndexInQuery] int entityIndex, in ResourceProducerComponent producer, in ResourceComponent producerResource)
    {
        // Perform the calculation
        var resource = ResourceLookup[producer.ProducedResource];

        var result = producer.ProducedAmount; // Copy the value
        result.MultiplyBigNum(producerResource.Amount); // Multiply by the producer resource amount
        result.AddBigNum(resource.Amount); // Add the current resource amount

        // Store the result
        Results[entityIndex] = result;
    }
}


// New job to update LastProductionTime
[BurstCompile]
public partial struct UpdateLastProductionTimeJob : IJobEntity
{
    private void Execute(ref TickerComponent ticker)
    {
        ticker.LastTick += ticker.TickInterval;
    }
}

