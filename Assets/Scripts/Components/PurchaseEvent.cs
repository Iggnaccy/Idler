using Unity.Entities;

public struct PurchaseEvent : IComponentData
{
    public Entity Entity;
    public PurchaseType Type;
    public enum PurchaseType
    {
        Producer,
        Upgrade
    }
}
