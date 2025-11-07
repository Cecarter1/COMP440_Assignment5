using UnityEngine;

public class AbilityManager : MonoBehaviour
{
    // Make this script persistent across scenes (Singleton Pattern)
    public static AbilityManager Instance;

    [Header("Ability Status")]
    public bool isDashUnlocked = false;
    public bool isDoubleJumpUnlocked = false;
    public bool isGravityFlipUnlocked = false;

    private void Awake()
    {
        // 1. Singleton setup: Ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep this object when loading new levels
        }
        else
        {
            Destroy(gameObject);
        }

        // 2. Initialize abilities to be locked at startup
        LockAllAbilities();
    }

    // Call this only once when the game starts (in Awake)
    public void LockAllAbilities()
    {
        // Find the PlayerController reference and lock abilities
        // NOTE: The player must exist in the scene when this is called, 
        // or you may need to call this again when the player loads.
        PlayerController player = FindObjectOfType<PlayerController>();

        if (player != null)
        {
            // Set all flags in the PlayerController to false
            player.enableDash = false;
            player.enableUnlimitedAirJumps = false;
            // The gravity ability is likely controlled externally, but we control its use here:
            // player.canFlipGravity = false;
        }
    }

    // Public method called by the collectible items
    public void UnlockAbility(string abilityName)
    {
        PlayerController player = FindObjectOfType<PlayerController>();

        if (player == null) return; // Exit if player isn't loaded yet

        switch (abilityName)
        {
            case "Dash":
                if (!isDashUnlocked)
                {
                    isDashUnlocked = true;
                    player.enableDash = true;
                    Debug.Log("Ability Unlocked: Dash");
                }
                break;

            case "DoubleJump":
                if (!isDoubleJumpUnlocked)
                {
                    isDoubleJumpUnlocked = true;
                    player.enableUnlimitedAirJumps = true; // Use your teammate's unlimited jump flag
                    // Note: You must remove the previous jump counter code to use this flag
                    Debug.Log("Ability Unlocked: Double Jump");
                }
                break;

            case "GravityFlip":
                if (!isGravityFlipUnlocked)
                {
                    isGravityFlipUnlocked = true;
                    // player.canFlipGravity = true;
                    Debug.Log("Ability Unlocked: Gravity Flip");
                }
                break;
        }
    }
}