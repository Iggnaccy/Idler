using Unity.Entities;

public struct TickerComponent : IComponentData
{
    public long LastTick;
    public long TickInterval;
}
