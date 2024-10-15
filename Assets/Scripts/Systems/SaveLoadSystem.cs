using System;
using System.IO;
using System.Text;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial class SaveLoadSystem : SystemBase
{
    public static readonly string SAVE_FILE_NAME = "savegame.sav";
    public static event Action<bool> OnSave, OnLoad;

    private EntityQuery saveEventQuery, loadEventQuery;
    private static ComponentLookup<ResourceComponent> resourceLookup;
    private static ComponentLookup<ResourceProducerComponent> resourceProducerLookup;
    private static ComponentLookup<TickerComponent> tickerLookup;

    protected override void OnCreate()
    {
        saveEventQuery = GetEntityQuery(ComponentType.ReadOnly<SaveEventComponent>());
        loadEventQuery = GetEntityQuery(ComponentType.ReadOnly<LoadEventComponent>());

        resourceLookup = GetComponentLookup<ResourceComponent>(false);
        resourceProducerLookup = GetComponentLookup<ResourceProducerComponent>(false);
        tickerLookup = GetComponentLookup<TickerComponent>(false);
    }

    protected override void OnUpdate()
    {
        if(saveEventQuery.CalculateEntityCount() > 0)
        {
            SaveGame();
            EntityManager.DestroyEntity(saveEventQuery.GetSingletonEntity());
        }

        if(loadEventQuery.CalculateEntityCount() > 0)
        {
            LoadGame();
            EntityManager.DestroyEntity(loadEventQuery.GetSingletonEntity());
        }
    }

    private void LoadGame()
    {
        byte[] bytes = File.ReadAllBytes(SAVE_FILE_NAME);

        // Conversion of byte array to save chunks hashmap per saveable component
        NativeHashMap<int, NativeList<byte>> saveChunks = new NativeHashMap<int, NativeList<byte>>(0, Allocator.TempJob);
        int i = 0;
        try
        {
            while (i < bytes.Length)
            {
                // Read saveable component
                int id;
                SaveableComponent.SaveableType type;
                (id, type) = ReadSaveable(bytes, ref i);
                Version saveVersion = ReadVersion(bytes, ref i);
                if(!IsSaveVersionCompatible(saveVersion))
                {
                    Debug.LogError($"Save version {saveVersion} is not compatible with current version {GameController.Version}");
                    OnLoad?.Invoke(false);
                    return;
                }
                NativeList<byte> list = new NativeList<byte>(Allocator.TempJob);
                switch (type)
                {
                    case SaveableComponent.SaveableType.Resource: // all ResourceComponents are 16 bytes = double2 in save data
                        ReadResourceComponent(bytes, ref i, ref list);
                        break;
                    case SaveableComponent.SaveableType.ResourceProducer: // all ResourceProducerComponents also have a ResourceComponent, so total size is 68 + 16 = 84 bytes = ((8 + 8) + (8 + 8) + 4) + ((8 + 8) + 4)
                        ReadResourceProducerComponent(bytes, ref i, ref list);
                        break;
                    case SaveableComponent.SaveableType.Ticker: // all TickerComponents are 16 bytes = 8 + 8 bytes
                        ReadTicker(bytes, ref i, ref list);
                        break;
                }
                if (!saveChunks.ContainsKey(id))
                {
                    saveChunks.Add(id, list);
                }
                else
                {
                    Debug.LogWarning($"Duplicate saveable component ID: {id}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading game: {e.Message}");
            OnLoad?.Invoke(false);
            return;
        }

        // update lookups:
        resourceLookup.Update(this);
        resourceProducerLookup.Update(this);
        tickerLookup.Update(this);

        var ResourceLookup = resourceLookup;
        var ResourceProducerLookup = resourceProducerLookup;
        var TickerLookup = tickerLookup;

        Entities
            .WithReadOnly(saveChunks)
            .WithNativeDisableContainerSafetyRestriction(saveChunks)
            .WithNativeDisableParallelForRestriction(ResourceLookup)
            .WithNativeDisableParallelForRestriction(ResourceProducerLookup)
            .WithNativeDisableParallelForRestriction(TickerLookup)
            .ForEach((Entity e, in SaveableComponent saveable) =>
        {
            var saveChunk = saveChunks[saveable.ID];
            switch (saveable.Type)
            {
                case SaveableComponent.SaveableType.Resource:
                    var component = ResourceLookup[e];
                    LoadResourceComponent(ref component, saveChunk);
                    ResourceLookup[e] = component;
                    break;
                case SaveableComponent.SaveableType.ResourceProducer:
                    var resource = ResourceLookup[e];
                    LoadResourceComponent(ref resource, saveChunk);
                    ResourceLookup[e] = resource;
                    var producer = ResourceProducerLookup[e];
                    LoadResourceProducerComponent(ref producer, saveChunk);
                    ResourceProducerLookup[e] = producer;
                    break;
                case SaveableComponent.SaveableType.Ticker:
                    var ticker = TickerLookup[e];
                    LoadTicker(ref ticker, saveChunk);
                    TickerLookup[e] = ticker;
                    break;
            }
        }).ScheduleParallel(Dependency).Complete();

        foreach (var kvp in saveChunks)
        {
            kvp.Value.Dispose();
        }
        saveChunks.Dispose();

        OnLoad?.Invoke(true);
    }

    private bool IsSaveVersionCompatible(Version saveVersion)
    {
        return saveVersion.Major == GameController.LastSupportedSaveVersion.Major && saveVersion.Minor == GameController.LastSupportedSaveVersion.Minor;
    }

    private static Version ReadVersion(in byte[] bytes, ref int i)
    {
        Assert.IsTrue(i + 3 * sizeof(int) <= bytes.Length); // 3 * 4 = 12 bytes
        int major = BitConverter.ToInt32(bytes, i);
        int minor = BitConverter.ToInt32(bytes, i + sizeof(int));
        int build = BitConverter.ToInt32(bytes, i + 2 * sizeof(int));
        i += 3 * sizeof(int);
        return new Version(major, minor, build);
    }

    private static (int, SaveableComponent.SaveableType) ReadSaveable(in byte[] bytes, ref int i)
    {
        Assert.IsTrue(i + 1 <= bytes.Length); // 1 = 5 bits + 3 bits
        byte save = bytes[i];
        int id = save >> 3;
        SaveableComponent.SaveableType type = (SaveableComponent.SaveableType)(save & 0b111);
        i++;
        return (id, type);
    }

    private static void ReadTicker(in byte[] bytes, ref int i, ref NativeList<byte> list)
    {
        Assert.IsTrue(i + 16 <= bytes.Length); // 16 = 8 (long) * 2
        // add 16 bytes for the ticker component to the save chunk
        for (int j = 0; j < 16; j++)
        {
            list.Add(bytes[i + j]);
        }
        // move i to the next saveable component
        i += 16;
    }

    private static void ReadResourceProducerComponent(in byte[] bytes, ref int i, ref NativeList<byte> list)
    {
        Assert.IsTrue(i + 84 <= bytes.Length); // 84 = 16 (ResourceComponent) + 68 (ResourceProducerComponent)
        // add 84 bytes for the resource producer component to the save chunk
        for (int j = 0; j < 84; j++)
        {
            list.Add(bytes[i + j]);
        }
        // move i to the next saveable component
        i += 84;
    }

    private static void ReadResourceComponent(in byte[] bytes, ref int i, ref NativeList<byte> list)
    {
        Assert.IsTrue(i + 16 <= bytes.Length); // 16 = 8 (double) * 2
        // add 16 bytes for the resource component to the save chunk
        for (int j = 0; j < 16; j++)
        {
            list.Add(bytes[i + j]);
        }
        // move i to the next saveable component
        i += 16;
    }

    private void SaveGame()
    {
        NativeList<byte> saveData = new NativeList<byte>(Allocator.TempJob);
        WriteVersion(saveData);
        try
        {
            Entities.ForEach((Entity e, in SaveableComponent saveable) =>
            {
                WriteSaveableComponent(saveable, saveData);
                switch (saveable.Type)
                {
                    case SaveableComponent.SaveableType.Resource:
                        WriteResourceComponent(EntityManager.GetComponentData<ResourceComponent>(e), saveData);
                        break;
                    case SaveableComponent.SaveableType.ResourceProducer:
                        WriteResourceComponent(EntityManager.GetComponentData<ResourceComponent>(e), saveData);
                        WriteResourceProducerComponent(EntityManager.GetComponentData<ResourceProducerComponent>(e), saveData);
                        break;
                    case SaveableComponent.SaveableType.Ticker:
                        WriteTickerComponent(EntityManager.GetComponentData<TickerComponent>(e), saveData);
                        break;
                }
            }).WithoutBurst().Run();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving game: {e.Message}");
            OnSave?.Invoke(false);
        }

        File.WriteAllBytes(SAVE_FILE_NAME, saveData.ToArrayNBC());
        saveData.Dispose();

        OnSave?.Invoke(true);
    }

    private void WriteVersion(in NativeList<byte> saveData)
    {
        byte[] bytes = new byte[3 * sizeof(int)];
        BitConverter.GetBytes(GameController.Version.Major).CopyTo(bytes, 0);
        BitConverter.GetBytes(GameController.Version.Minor).CopyTo(bytes, sizeof(int));
        BitConverter.GetBytes(GameController.Version.Build).CopyTo(bytes, 2 * sizeof(int));
        for(int i = 0; i < bytes.Length; i++)
        {
            saveData.Add(bytes[i]);
        }
    }

    private static void WriteSaveableComponent(in SaveableComponent saveable, in NativeList<byte> saveData)
    {
        // these are small, we can fit them both in 1 byte
        byte save = (byte)(saveable.ID << 3); // 5 bits for ID
        save |= (byte)saveable.Type; // 3 bits for type
        saveData.Add(save);
    }

    private static void WriteResourceComponent(in ResourceComponent resource, in NativeList<byte> saveData)
    {
        WriteDouble2(resource.Amount, saveData); // 8*2 = 16 bytes
        // total size: 8*2 = 16 bytes
    }

    private static void WriteResourceProducerComponent(in ResourceProducerComponent producer, in NativeList<byte> saveData)
    {
        WriteDouble2(producer.ProducedAmount, saveData); // 8*2 = 16 bytes
        WriteDouble2(producer.NextPurchaseCost, saveData); // 8*2 = 16 bytes
        WriteDouble2(producer.PurchaseMultiplier, saveData); // 8*2 = 16 bytes
        WriteDouble2(producer.NextPurchaseBarrier, saveData); // 8*2 = 16 bytes
        WriteInt(producer.PurchaseBarriersPassed, saveData); // 4 bytes
        // total size: 8*2 * 4 + 4 = 68 bytes
    }

    private static void WriteTickerComponent(in TickerComponent ticker, in NativeList<byte> saveData)
    {
        WriteLong(ticker.LastTick, saveData); // 8 bytes
        WriteLong(ticker.TickInterval, saveData); // 8 bytes
        // total size: 8 * 2 = 16 bytes
    }

    private static void WriteInt(int value, in NativeList<byte> saveData)
    {
        var bytes = BitConverter.GetBytes(value);
        foreach (var b in bytes)
        {
            saveData.Add(b);
        }
    }

    private static void WriteDouble(double value, in NativeList<byte> saveData)
    {
        var bytes = BitConverter.GetBytes(value);
        foreach (var b in bytes)
        {
            saveData.Add(b);
        }
    }

    private static void WriteLong(long value, in NativeList<byte> saveData)
    {
        var bytes = BitConverter.GetBytes(value);
        foreach (var b in bytes)
        {
            saveData.Add(b);
        }
    }

    private static void WriteDouble2(double2 value, in NativeList<byte> saveData)
    {
        WriteDouble(value.x, saveData);
        WriteDouble(value.y, saveData);
    }

    private static void LoadResourceComponent(ref ResourceComponent resource, in NativeList<byte> saveData, int byteOffset = 0)
    {
        resource.Amount = ReadDouble2(saveData, byteOffset);
    }

    private static void LoadResourceProducerComponent(ref ResourceProducerComponent resourceProducer, in NativeList<byte> saveData, int byteOffset = 0)
    {
        resourceProducer.ProducedAmount = ReadDouble2(saveData, byteOffset);
        resourceProducer.NextPurchaseCost = ReadDouble2(saveData, byteOffset + 16);
        resourceProducer.PurchaseMultiplier = ReadDouble2(saveData, byteOffset + 32);
        resourceProducer.NextPurchaseBarrier = ReadDouble2(saveData, byteOffset + 48);
        resourceProducer.PurchaseBarriersPassed = ReadInt(saveData, byteOffset + 64);
    }

    private static void LoadTicker(ref TickerComponent tickerComponent, in NativeList<byte> saveData, int byteOffset = 0)
    {
        tickerComponent.LastTick = ReadLong(saveData, byteOffset);
        tickerComponent.TickInterval = ReadLong(saveData, byteOffset + 8);
    }

    private static double ReadDouble(in NativeList<byte> saveData, int byteOffset = 0)
    {
        long longValue = ReadLong(saveData, byteOffset);
        return BitConverter.Int64BitsToDouble(longValue);
    }
    private static long ReadLong(in NativeList<byte> saveData, int byteOffset = 0)
    {
        long value = ((long)saveData[byteOffset] << 0) |
                     ((long)saveData[byteOffset + 1] << 8) |
                     ((long)saveData[byteOffset + 2] << 16) |
                     ((long)saveData[byteOffset + 3] << 24) |
                     ((long)saveData[byteOffset + 4] << 32) |
                     ((long)saveData[byteOffset + 5] << 40) |
                     ((long)saveData[byteOffset + 6] << 48) |
                     ((long)saveData[byteOffset + 7] << 56);

        return value;
    }
    private static int ReadInt(in NativeList<byte> saveData, int byteOffset = 0)
    {
        int value = (saveData[byteOffset] << 0) |
                    (saveData[byteOffset + 1] << 8) |
                    (saveData[byteOffset + 2] << 16) |
                    (saveData[byteOffset + 3] << 24);

        return value;
    }
    private static double2 ReadDouble2(in NativeList<byte> saveData, int byteOffset = 0)
    {
        double x = ReadDouble(saveData, byteOffset);
        double y = ReadDouble(saveData, byteOffset + 8);
        return new double2(x, y);
    }
}
