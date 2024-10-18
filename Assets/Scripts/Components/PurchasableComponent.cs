using Unity.Entities;
using Unity.Mathematics;

public struct PurchasableComponent : IComponentData
{
    public Entity CostCurrency;
    public double2 NextCostAmount;
    public double2 CostMultiplier;
    public double2 NextCostBarrier;
    public int CostBarriersPassed;
}
