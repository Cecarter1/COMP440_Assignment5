using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PowerupModifier : AbilityModifier
{
    public float powerupDeactivateSec;
    public float powerupRespawnSec;
    public abstract void Deactivate(GameObject target);
    public abstract void Respawn(GameObject powerup);

    public IEnumerator StartPowerupCountdown(GameObject target, GameObject powerup)
    {
        yield return new WaitForSeconds(powerupDeactivateSec);
        Deactivate(target);
        yield return new WaitForSeconds(powerupRespawnSec);
        Respawn(powerup);
    }
}
