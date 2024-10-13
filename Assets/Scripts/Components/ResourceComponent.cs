using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ResourceComponent : IComponentData
{
    private double2 amount;

    public double2 Amount 
    { 
        get => amount;
        set
        {
            if (!value.Equals(amount))
            {
                amount = value;
                IsDirty = true;
            }
        }
    }

    public bool IsDirty { get; set; }
}
