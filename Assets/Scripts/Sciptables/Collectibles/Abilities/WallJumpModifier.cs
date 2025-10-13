using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ability/Wall Jump")]
public class WallJumpModifier : AbilityModifier
{
    TestPlayerMovement playerMovement;
    public override void Activate(GameObject target)
    {
        playerMovement = target.GetComponent<TestPlayerMovement>();
        playerMovement.canWallJump = true;
    }
}
