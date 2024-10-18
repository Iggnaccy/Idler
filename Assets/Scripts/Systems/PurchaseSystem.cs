using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public partial class PurchaseSystem : SystemBase
{
    private EntityQuery m_PurchaseEventQuery;
    private ComponentLookup<PurchasableComponent> m_PurchasableLookup;
    private ComponentLookup<ResourceComponent> m_ResourceLookup;
    private ComponentLookup<ResourceProducerComponent> m_ProducerLookup;
    private ComponentLookup<UpgradeComponent> m_UpgradeLookup;

    override protected void OnCreate()
    {
        m_PurchaseEventQuery = GetEntityQuery(ComponentType.ReadOnly<PurchaseEvent>());
        m_PurchasableLookup = GetComponentLookup<PurchasableComponent>();
        m_ResourceLookup = GetComponentLookup<ResourceComponent>();
        m_ProducerLookup = GetComponentLookup<ResourceProducerComponent>();
        m_UpgradeLookup = GetComponentLookup<UpgradeComponent>();
    }

    protected override void OnUpdate()
    {
        if(m_PurchaseEventQuery.CalculateEntityCount() == 0)
        {
            return;
        }

        m_PurchasableLookup.Update(this);
        m_ResourceLookup.Update(this);
        m_ProducerLookup.Update(this);
        m_UpgradeLookup.Update(this);

        var purchasableLookup = m_PurchasableLookup;
        var resourceLookup = m_ResourceLookup;
        var producerLookup = m_ProducerLookup;
        var upgradeLookup = m_UpgradeLookup;

        Entities
            .WithReadOnly(producerLookup)
            .ForEach((in PurchaseEvent purchaseEvent) =>
            {
                var purchasable = purchasableLookup[purchaseEvent.Entity];
                if (purchaseEvent.Type == PurchaseEvent.PurchaseType.Producer)
                {
                    var resource = resourceLookup[purchaseEvent.Entity];
                    var producer = producerLookup[purchaseEvent.Entity];
                    var requiredResource = resourceLookup[purchasable.CostCurrency];
                    if (requiredResource.Amount.IsBigNumGreaterThan(purchasable.NextCostAmount))
                    {
                        requiredResource.Amount = requiredResource.Amount.SubtractBigNumR(purchasable.NextCostAmount);
                        var newCost = purchasable.NextCostAmount.MultiplyBigNumR(purchasable.CostMultiplier);

                        if (newCost.IsBigNumGreaterThan(purchasable.NextCostBarrier))
                        {
                            // NYI: Set the barrier to the next level
                            // purchasable.NextCostBarrier = GetNextCostBarrier(in saveable, purchasable.CostBarriersPassed);
                            // Temporary - move barrier up by e100
                            purchasable.NextCostBarrier = purchasable.NextCostBarrier.MultiplyBigNumR(new Unity.Mathematics.double2(1, 100));
                            purchasable.CostBarriersPassed++;
                            purchasable.CostMultiplier = purchasable.CostMultiplier.MultiplyBigNumR(new Unity.Mathematics.double2(1, 1));

                            purchasableLookup[purchaseEvent.Entity] = purchasable;
                        }

                        resourceLookup[purchasable.CostCurrency] = requiredResource; // required resource will often overlap between possible purchases

                        purchasable.NextCostAmount = newCost;
                        purchasableLookup[purchaseEvent.Entity] = purchasable;
                    }
                }
                else
                {
                    var requiredResource = resourceLookup[purchasable.CostCurrency];
                    var upgrade = upgradeLookup[purchaseEvent.Entity];
                    if(!upgrade.IsBought && requiredResource.Amount.IsBigNumGreaterThan(purchasable.NextCostAmount))
                    {
                        requiredResource.Amount = requiredResource.Amount.SubtractBigNumR(purchasable.NextCostAmount);
                        resourceLookup[purchasable.CostCurrency] = requiredResource; // required resource will often overlap between possible purchases

                        upgrade.IsBought = true;
                        upgradeLookup[purchaseEvent.Entity] = upgrade;

                        var newCost = purchasable.NextCostAmount.MultiplyBigNumR(purchasable.CostMultiplier);
                        purchasable.NextCostAmount = newCost;
                        purchasableLookup[purchaseEvent.Entity] = purchasable;
                    }
                }
            }).Run();

        EntityManager.DestroyEntity(m_PurchaseEventQuery);
    }
}
