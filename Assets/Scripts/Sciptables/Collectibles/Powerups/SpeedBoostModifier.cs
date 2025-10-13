using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Powerup/Speed Boost Modifier")]
public class SpeedBoostModifier : PowerupModifier
{
    TestPlayerMovement playerMovement;

    public int speedValue;
    public override void Activate(GameObject target)
    {
        playerMovement = target.GetComponent<TestPlayerMovement>();
        playerMovement.AddSpeed(speedValue);
    }

    public override void Deactivate(GameObject target)
    {
        playerMovement.AddSpeed(-speedValue);
        playerMovement.isPowerupActive = false;
    }

    public override void Respawn(GameObject powerup)
    {
        powerup.SetActive(true);
    }
}
