using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Ability/Double Jump")]
public class DoubleJumpModifier : AbilityModifier
{
    TestPlayerMovement playerMovement;
    public override void Activate(GameObject target)
    {
        playerMovement = target.GetComponent<TestPlayerMovement>();
        playerMovement.canDoubleJump = true;
    }
}
