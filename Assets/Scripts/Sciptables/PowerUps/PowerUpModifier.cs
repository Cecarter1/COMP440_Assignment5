using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PowerUpModifier : AbilityModifier
{
    public float powerupSec;
    public abstract void Deactivate(GameObject target);

    public IEnumerator StartPowerupCountdown(GameObject target)
    {
        yield return new WaitForSeconds(powerupSec);
        Deactivate(target);
    }
}
