using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ResourceProducerComponent : IComponentData
{
    public Entity ProducedResource { get; set; }
    public double2 ProducedAmount { get; set; }
}
