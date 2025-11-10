using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ability/Gravity")]
public class GravityModifier : AbilityModifier
{
    // Caches the correct PlayerController component
    PlayerController playerController;

    public override void Activate(GameObject target)
    {
        // 1. Get the correct component
        playerController = target.GetComponent<PlayerController>();

        if (playerController != null)
        {
            // 2. Set the public flag in PlayerController to unlock the ability
            playerController.canGravity = true;
        }
    }
    // Since this is a permanent unlock (AbilityModifier), no Deactivate is needed.
}
