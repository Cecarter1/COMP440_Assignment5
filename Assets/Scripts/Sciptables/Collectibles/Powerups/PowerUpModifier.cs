using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PowerupModifier : AbilityModifier
{
    public float powerupDeactivateSec = 10;
    public float powerupRespawnSec = 45;

    public abstract void Deactivate(GameObject target);
    public abstract void Respawn(GameObject powerup);

    public IEnumerator StartPowerupCountdown(GameObject target, GameObject powerup)
    {
        yield return new WaitForSeconds(powerupDeactivateSec);
        Deactivate(target);
        yield return new WaitForSeconds(powerupRespawnSec);
        Respawn(powerup);
    }

    public IEnumerator Despawn(GameObject powerup)
    {
        yield return new WaitForSeconds(powerupRespawnSec);
        Respawn(powerup);
    }
}
