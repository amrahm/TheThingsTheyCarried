﻿using UnityEngine;

public abstract class MovementAbstract : MonoBehaviour {
    /// <summary> Which way the character is currently facing </summary>
    public abstract bool FacingRight { get; set; }

    /// <summary> Is the player currently on the ground </summary>
    public abstract bool Grounded { get; set; }
    
    /// <summary> The direction that the character wants to move </summary>
    public abstract Vector2 MoveVec { get; set; }

    /// <summary> The direction that the character wants to move </summary>
    public abstract float MaxWalkSlope { get; set; }

}