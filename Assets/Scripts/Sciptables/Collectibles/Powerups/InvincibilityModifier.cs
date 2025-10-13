using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Powerup/Invincibility Modifier")]
public class InvincibilityModifier : PowerupModifier
{
    TestPlayerHealth playerHealth;
    TestPlayerMovement playerMovement;
    public override void Activate(GameObject target)
    {
        playerMovement = target.GetComponent<TestPlayerMovement>();
        playerHealth = target.GetComponent<TestPlayerHealth>();
        playerHealth.isInvincible = true;
    }

    public override void Deactivate(GameObject target)
    {
        playerHealth.isInvincible = false;
        playerMovement.isPowerupActive = false;
    }

    public override void Respawn(GameObject powerup)
    {
        powerup.SetActive(true);
    }
}
