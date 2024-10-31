using Unity.Entities;

public struct PurchaseEvent : IComponentData
{
    public Entity Entity;
    public PurchaseType Type;
    public PurchaseSubType SubType;
    public enum PurchaseType
    {
        Producer,
        Upgrade
    }

    public enum PurchaseSubType
    {
        Single,
        Max
    }
}
