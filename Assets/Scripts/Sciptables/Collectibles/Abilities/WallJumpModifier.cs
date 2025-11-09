using UnityEngine;

[CreateAssetMenu(menuName = "Ability/Wall Jump")]
public class WallJumpModifier : AbilityModifier
{
    // Caches the correct PlayerController component
    PlayerController playerController;

    public override void Activate(GameObject target)
    {
        // 1. Get the correct component (PlayerController)
        playerController = target.GetComponent<PlayerController>();

        if (playerController != null)
        {
            // 2. Set the public flag in PlayerController to unlock the ability
            playerController.canWallJump = true;
        }
    }
}
