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
    private static ComponentLookup<PurchasableComponent> purchasableLookup;
    private static EntityQuery upgradeableQuery;
    private static ComponentLookup<TickerComponent> tickerLookup;

    protected override void OnCreate()
    {
        saveEventQuery = GetEntityQuery(ComponentType.ReadOnly<SaveEventComponent>());
        loadEventQuery = GetEntityQuery(ComponentType.ReadOnly<LoadEventComponent>());

        resourceLookup = GetComponentLookup<ResourceComponent>(false);
        resourceProducerLookup = GetComponentLookup<ResourceProducerComponent>(false);
        purchasableLookup = GetComponentLookup<PurchasableComponent>(false);
        tickerLookup = GetComponentLookup<TickerComponent>(false);
        upgradeableQuery = GetEntityQuery(typeof(UpgradeComponent));
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
        NativeArray<byte> upgradesBitTable = new NativeArray<byte>(0, Allocator.None);
        int i = 0;
        try
        {
            // Read and validate save version once at the beginning of the file
            Version saveVersion = ReadVersion(bytes, ref i);
            if (!IsSaveVersionCompatible(saveVersion))
            {
                Debug.LogError($"Save version {saveVersion} is not compatible with current version {GameController.Version}");
                OnLoad?.Invoke(false);
                return;
            }

            while (i < bytes.Length)
            {
                // Read saveable component
                int id;
                SaveableComponent.SaveableType type;
                (id, type) = ReadSaveable(bytes, ref i);
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
                    case SaveableComponent.SaveableType.Upgrade:
                        ReadUpgrades(bytes, ref i, ref list, out upgradesBitTable);
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
        purchasableLookup.Update(this);
        tickerLookup.Update(this);

        var ResourceLookup = resourceLookup;
        var ResourceProducerLookup = resourceProducerLookup;
        var PurchasableLookup = purchasableLookup;
        var TickerLookup = tickerLookup;

        Entities
            .WithReadOnly(saveChunks)
            .WithNativeDisableContainerSafetyRestriction(saveChunks)
            .WithNativeDisableParallelForRestriction(ResourceLookup)
            .WithNativeDisableParallelForRestriction(ResourceProducerLookup)
            .WithNativeDisableParallelForRestriction(PurchasableLookup)
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
                    LoadResourceProducerComponent(ref producer, saveChunk, byteOffset: 16); // 16 = 8*2 = sizeof(double2)
                    ResourceProducerLookup[e] = producer;
                    var purchasable = PurchasableLookup[e];
                    LoadPurchasableComponent(ref purchasable, saveChunk, byteOffset: 32); // 32 = 16*2 = sizeof(double2) * 2
                    PurchasableLookup[e] = purchasable;
                    break;
                case SaveableComponent.SaveableType.Ticker:
                    var ticker = TickerLookup[e];
                    LoadTicker(ref ticker, saveChunk);
                    TickerLookup[e] = ticker;
                    break;
                case SaveableComponent.SaveableType.Upgrade:
                    // Handled separately, because all upgrades are in a single bitTable
                    break;
            }
        }).ScheduleParallel(Dependency).Complete();

        if (upgradesBitTable.Length > 0)
        {
            Entities
                .WithReadOnly(upgradesBitTable)
                .ForEach((ref UpgradeComponent upgradeable) =>
            {
                int index = upgradeable.UpgradeId / 8;
                int bit = upgradeable.UpgradeId % 8;
                byte mask = (byte)(1 << bit);
                upgradeable.IsBought = (upgradesBitTable[index] & mask) != 0;
            }).ScheduleParallel(Dependency).Complete();
        }
        else
        {
            Debug.LogWarning("No upgrades found in save file");
        }

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
        Assert.IsTrue(i + 2 <= bytes.Length); // Ensure there are at least 2 bytes to read (16 bits)

        // Read the two bytes and combine them into a single 16-bit value
        ushort save = (ushort)(bytes[i] | (bytes[i + 1] << 8));

        // Extract the 13-bit ID by shifting the save 3 bits to the right
        int id = save >> 3;

        // Extract the 3-bit type using a mask of 0b111 (binary for 7)
        SaveableComponent.SaveableType type = (SaveableComponent.SaveableType)(save & 0b111);

        // Increment index by 2, since we read 2 bytes
        i += 2;

        return (id, type);
    }


    private static void ReadUpgrades(in byte[] bytes, ref int i, ref NativeList<byte> list, out NativeArray<byte> bitTable)
    {
        Assert.IsTrue(i + 1 <= bytes.Length); // 1 = 8 bits
        byte numUpgrades = bytes[i];
        i++;
        Assert.IsTrue(i + numUpgrades <= bytes.Length); // numUpgrades = 8 bits
        bitTable = new NativeArray<byte>(numUpgrades, Allocator.TempJob);
        for (int j = 0; j < numUpgrades; j++)
        {
            bitTable[j] = bytes[i + j];
        }
        i += numUpgrades;
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
        Assert.IsTrue(i + 84 <= bytes.Length); // 84 = 16 (ResourceComponent) + 16 (ResourceProducerComponent) + 52 (PurchasableComponent)
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
        SaveableComponent upgradeSaveable = new SaveableComponent { ID = -1, Type = SaveableComponent.SaveableType.Upgrade };

        resourceLookup.Update(this);
        resourceProducerLookup.Update(this);
        purchasableLookup.Update(this);
        tickerLookup.Update(this);

        var ResourceLookup = resourceLookup;
        var ResourceProducerLookup = resourceProducerLookup;
        var PurchasableLookup = purchasableLookup;
        var TickerLookup = tickerLookup;

        WriteVersion(saveData);
        try
        {
            Entities
                .WithReadOnly(ResourceLookup)
                .WithNativeDisableParallelForRestriction(ResourceLookup)
                .WithReadOnly(ResourceProducerLookup)
                .WithNativeDisableParallelForRestriction(ResourceProducerLookup)
                .WithReadOnly(PurchasableLookup)
                .WithNativeDisableParallelForRestriction(PurchasableLookup)
                .WithReadOnly(TickerLookup)
                .WithNativeDisableParallelForRestriction(TickerLookup)
                .ForEach((Entity e, in SaveableComponent saveable) =>
            {
                WriteSaveableComponent(saveable, saveData);
                switch (saveable.Type)
                {
                    case SaveableComponent.SaveableType.Resource:
                        var resource = ResourceLookup[e];
                        WriteResourceComponent(resource, saveData);
                        break;
                    case SaveableComponent.SaveableType.ResourceProducer:
                        var resourceP = ResourceLookup[e];
                        var resourceProducer = ResourceProducerLookup[e];
                        var purchasable = PurchasableLookup[e];
                        WriteResourceComponent(resourceP, saveData);
                        WriteResourceProducerComponent(resourceProducer, saveData);
                        WritePurchasableComponent(purchasable, saveData);
                        break;
                    case SaveableComponent.SaveableType.Ticker:
                        var ticker = TickerLookup[e];
                        WriteTickerComponent(ticker, saveData);
                        break;
                    case SaveableComponent.SaveableType.Upgrade:
                        upgradeSaveable = saveable;
                        // Handled separately, because all upgrades are saved in a single bitTable
                        break;
                }
            }).Run();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving game: {e.Message}");
            OnSave?.Invoke(false);
        }

        // handle upgrades separately:
        int numUpgrades = upgradeableQuery.CalculateEntityCount();
        NativeArray<byte> bitTable = new NativeArray<byte>((numUpgrades + 7) / 8, Allocator.TempJob);
        Entities
            .ForEach((Entity e, in UpgradeComponent upgradeable) =>
        {
            int index = upgradeable.UpgradeId / 8;
            int bit = upgradeable.UpgradeId % 8;
            byte mask = (byte)(1 << bit);
            if (upgradeable.IsBought)
            {
                bitTable[index] |= mask;
            }
        }).Run();

        WriteSaveableComponent(upgradeSaveable, saveData);
        saveData.Add((byte)numUpgrades); // we can only use 1 byte for the number of upgrades because we have a max of 255 upgrades
        for (int i = 0; i < bitTable.Length; i++)
        {
            saveData.Add(bitTable[i]);
        }
        bitTable.Dispose();

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
        // 13 bits for ID and 3 bits for Type
        ushort save = (ushort)(saveable.ID << 3); // Shift the ID left by 3 to make space for the 3 bits for Type
        save |= (ushort)((int)saveable.Type & 0b111); // Mask Type with 0b111 to ensure it fits in 3 bits
        saveData.Add((byte)(save & 0xFF)); // Add the lower 8 bits
        saveData.Add((byte)((save >> 8) & 0xFF)); // Add the upper 8 bits
    }


    private static void WriteResourceComponent(in ResourceComponent resource, in NativeList<byte> saveData)
    {
        WriteDouble2(resource.Amount, saveData); // 8*2 = 16 bytes
        // total size: 8*2 = 16 bytes
    }

    private static void WriteResourceProducerComponent(in ResourceProducerComponent producer, in NativeList<byte> saveData)
    {
        WriteDouble2(producer.ProducedAmount, saveData); // 8*2 = 16 bytes
        // total size: 8*2 = 16 bytes
    }

    private static void WritePurchasableComponent(in PurchasableComponent purchasable, in NativeList<byte> saveData)
    {
        WriteDouble2(purchasable.NextCostAmount, saveData); // 8*2 = 16 bytes
        WriteDouble2(purchasable.CostMultiplier, saveData); // 8*2 = 16 bytes
        WriteDouble2(purchasable.NextCostBarrier, saveData); // 8*2 = 16 bytes
        WriteInt(purchasable.CostBarriersPassed, saveData); // 4 bytes
        // total size: 8*6 + 4= 52 bytes
    }

    private static void WriteTickerComponent(in TickerComponent ticker, in NativeList<byte> saveData)
    {
        WriteLong(ticker.LastTick, saveData); // 8 bytes
        WriteLong(ticker.TickInterval, saveData); // 8 bytes
        // total size: 8 * 2 = 16 bytes
    }

    private static void WriteInt(int value, in NativeList<byte> saveData)
    {
        saveData.Add((byte)(value & 0xFF));
        saveData.Add((byte)((value >> 8) & 0xFF));
        saveData.Add((byte)((value >> 16) & 0xFF));
        saveData.Add((byte)((value >> 24) & 0xFF));
    }

    private static void WriteDouble(double value, in NativeList<byte> saveData)
    {
        long longValue = BitConverter.DoubleToInt64Bits(value);
        WriteLong(longValue, saveData);
    }

    private static void WriteLong(long value, in NativeList<byte> saveData)
    {
        saveData.Add((byte)(value & 0xFF));
        saveData.Add((byte)((value >> 8) & 0xFF));
        saveData.Add((byte)((value >> 16) & 0xFF));
        saveData.Add((byte)((value >> 24) & 0xFF));
        saveData.Add((byte)((value >> 32) & 0xFF));
        saveData.Add((byte)((value >> 40) & 0xFF));
        saveData.Add((byte)((value >> 48) & 0xFF));
        saveData.Add((byte)((value >> 56) & 0xFF));
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
        //resourceProducer.NextPurchaseCost = ReadDouble2(saveData, byteOffset + 16);
        //resourceProducer.PurchaseMultiplier = ReadDouble2(saveData, byteOffset + 32);
        //resourceProducer.NextPurchaseBarrier = ReadDouble2(saveData, byteOffset + 48);
        //resourceProducer.PurchaseBarriersPassed = ReadInt(saveData, byteOffset + 64);
    }

    private static void LoadPurchasableComponent(ref PurchasableComponent purchasable, in NativeList<byte> saveData, int byteOffset = 0)
    {
        purchasable.NextCostAmount = ReadDouble2(saveData, byteOffset);
        purchasable.CostMultiplier = ReadDouble2(saveData, byteOffset + 16);
        purchasable.NextCostBarrier = ReadDouble2(saveData, byteOffset + 32);
        purchasable.CostBarriersPassed = ReadInt(saveData, byteOffset + 48);
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
