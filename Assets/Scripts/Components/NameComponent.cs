using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

public struct NameComponent : IComponentData
{
    public NativeArray<char> Name { get; }

    public NameComponent(string name)
    {
        Name = new NativeArray<char>(name.ToCharArray(), Allocator.Persistent);
    }

    public readonly JobHandle Dispose(JobHandle inputDeps)
    {
        Dispose();
        return inputDeps;
    }

    public readonly void Dispose()
    {
        Name.Dispose();
    }

    public readonly override string ToString() => new string(Name.ToArray());
}
