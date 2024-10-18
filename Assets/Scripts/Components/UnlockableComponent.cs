using Unity.Entities;
using Unity.Mathematics;

public struct UnlockableComponent : IComponentData
{
    public Entity Target;
    public double2 AmountToReach;
    public bool IsUnlocked;
}
