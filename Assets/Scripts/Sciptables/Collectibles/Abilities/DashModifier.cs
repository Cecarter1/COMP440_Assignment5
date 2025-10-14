using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "Ability/Dash")]
public class DashModifier : AbilityModifier
{
    TestPlayerMovement playerMovement;

    public override void Activate(GameObject target)
    {
        playerMovement = target.GetComponent<TestPlayerMovement>();
        playerMovement.EnableDash();
    }
}
