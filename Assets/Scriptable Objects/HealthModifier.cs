using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Powerup/Health Modifier")]
public class HealthModifier : PowerupModifier
{
    public int healthValue;

    public override void Activate(GameObject target)
    {
        var playerHealth = target.GetComponent<TestPlayerHealth>();
        playerHealth.health += healthValue;
    }

    public override void Deactivate(GameObject target)
    {
        
    }

    public override void Respawn(GameObject powerup)
    {
        powerup.SetActive(true);
    }
}
