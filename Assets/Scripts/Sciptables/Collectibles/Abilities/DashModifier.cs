using UnityEngine;

[CreateAssetMenu(menuName = "Ability/Dash")]
public class DashModifier : AbilityModifier
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
            playerController.canDash = true;
        }
    }
    // Since this is a permanent unlock (AbilityModifier), no Deactivate is needed.
}
