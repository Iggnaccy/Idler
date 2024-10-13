using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct UndirtyJob : IJobEntity
{
    public void Execute(ref ResourceComponent resource)
    {
        resource.IsDirty = false;
    }
}
