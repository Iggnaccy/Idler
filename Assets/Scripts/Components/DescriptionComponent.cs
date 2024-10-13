using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

public readonly struct DescriptionComponent : IComponentData, INativeDisposable
{
    public NativeArray<char> Description { get; }

    public DescriptionComponent(string description)
    {
        Description = new NativeArray<char>(description.ToCharArray(), Allocator.Persistent);
    }

    public readonly JobHandle Dispose(JobHandle inputDeps)
    {
        Dispose();
        return inputDeps;
    }

    public readonly void Dispose()
    {
        Description.Dispose();
    }
}
