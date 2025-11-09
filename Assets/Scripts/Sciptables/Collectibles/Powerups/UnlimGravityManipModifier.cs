using UnityEngine;

[CreateAssetMenu(menuName = "Ability/Unlimited Gravity Manip")]
public class UnlimGravityManipModifier : AbilityModifier // Changed to AbilityModifier
{
    // Caches the correct PlayerController component
    PlayerController playerController;

    public override void Activate(GameObject target)
    {
        // Get the correct component
        playerController = target.GetComponent<PlayerController>();

        if (playerController != null)
        {
            // Set the correct flag in PlayerController to unlock the feature
            playerController.hasUnlimitedGravity = true;

            // Optionally, set the main ability flag to true if the PlayerController needs it.
            // playerController.isPowerupActive = true; 
        }
    }

    // Deactivate and Respawn are removed, as the ability is PERMANENT.
    // The player never loses the ability once unlocked.
}