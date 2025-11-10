using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Powerup/Unlimited Gravity Manip Modifier")]
public class UnlimGravityManipModifier : PowerupModifier
{
    PlayerController playerMovement;
    public override void Activate(GameObject target)
    {
        playerMovement = target.GetComponent<PlayerController>();
        playerMovement.hasUnlimitedGravity = true;
    }

    public override void Deactivate(GameObject target)
    {
        playerMovement.hasUnlimitedGravity = false;
        playerMovement.isPowerupActive = false;
    }

    public override void Respawn(GameObject powerup)
    {
        powerup.SetActive(true);
    }
}