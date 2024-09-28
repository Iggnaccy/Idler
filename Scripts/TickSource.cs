using Godot;
using System;

[GlobalClass]
public partial class TickSource : Resource
{
    public event EventHandler<int> OnTick;

    public void DoTick(int count = 1)
    {
        OnTick?.Invoke(this, count);
    }
}
