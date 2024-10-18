using Unity.Entities;
using Unity.Mathematics;

public struct UpgradeComponent : IComponentData
{
    public bool IsBought; // saved
    public enum UpgradeType
    {
        Production,
        Cost,
        TickRate
    }
    public enum UpgradeSubType
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    public int UpgradeId; // not saved, used in loading
    public UpgradeType Type; // not saved
    public UpgradeSubType SubType; // not saved
    public Entity Target; // not saved
    public double2 Modifier; // not saved
}
