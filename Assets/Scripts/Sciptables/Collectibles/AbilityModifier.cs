using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AbilityModifier : ScriptableObject
{
    public abstract void Activate(GameObject target);
}
