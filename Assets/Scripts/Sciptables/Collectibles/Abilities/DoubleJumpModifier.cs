using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ability/Double Jump")]
public class DoubleJumpModifier : AbilityModifier
{
    // *** FIX 1: Change to the correct player script name ***
    PlayerController playerController;

    public override void Activate(GameObject target)
    {
        PlayerController playerController = target.GetComponent<PlayerController>();

        // Set the specific flag for the single jump ability
        playerController.canDoubleJump = true;

        // CRUCIAL: Ensure the unlimited jump flag is OFF, in case the designer left it on
        playerController.enableUnlimitedAirJumps = false;
    }
}
