using Godot;
using System;

public partial class TickController : Node
{
	[Export] private TickSource _tickSource;


    public override void _Ready()
    {

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
		// check if next tick should be triggered
	}
}
