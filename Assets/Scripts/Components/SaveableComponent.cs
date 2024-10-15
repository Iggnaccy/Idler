using Unity.Entities;

public struct SaveableComponent : IComponentData
{
    public SaveableType Type;
    public int ID;

    public enum SaveableType
    {
        Resource,
        ResourceProducer,
        Ticker
    }
}
