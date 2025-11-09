using UnityEngine;
using UnityEngine.SceneManagement; // CRITICAL: Required for scene events

public class AbilityManager : MonoBehaviour
{
    public static AbilityManager Instance;

    [Header("Ability Status")]
    public bool isDashUnlocked = false;
    public bool isDoubleJumpUnlocked = false;
    public bool isGravityFlipUnlocked = false;

    private void Awake()
    {
        // Singleton and persistence setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        // 1. Subscribe to the scene loaded event when the manager is enabled
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // 2. Unsubscribe when the manager is disabled
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 3. This method runs every time a scene finishes loading
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // IMPORTANT: We only need to re-apply abilities when loading a new level/scene.
        // The mode check isn't strictly necessary since you use LoadSceneAsync(), 
        // but checking for the Player is necessary.

        // Give the player a moment to spawn and initialize in the scene
        Invoke(nameof(ApplyPersistentAbilities), 0.1f);
    }

    // 4. Method to re-apply all abilities based on saved state
    private void ApplyPersistentAbilities()
    {
        // Find the player object in the newly loaded scene
        PlayerController player = FindObjectOfType<PlayerController>();

        if (player != null)
        {
            // --- Double Jump (Example of persistence) ---
            if (Instance.isDoubleJumpUnlocked)
            {
                player.canDoubleJump = true;
            }

            // --- Gravity Flip (The Fix) ---
            if (Instance.isGravityFlipUnlocked)
            {
                player.hasUnlimitedGravity = true;
                player.canFlipGravity = true; // Also ensure the cooldown flag is reset if needed
            }

            // Add checks for other persistent abilities (Dash, Wall Jump, etc.) here
            if (Instance.isDashUnlocked) player.canDash = true;
        }
    }

    // Public method called by the collectible items
    public void UnlockAbility(string abilityName)
    {
        PlayerController player = FindObjectOfType<PlayerController>();

        if (player == null) return;

        switch (abilityName)
        {
            case "Dash":
                if (!isDashUnlocked)
                {
                    isDashUnlocked = true;
                    player.canDash = true;
                    // ... Debug Log ...
                }
                break;

            case "DoubleJump":
                if (!isDoubleJumpUnlocked)
                {
                    isDoubleJumpUnlocked = true;
                    player.canDoubleJump = true; // Sets the flag on the current player instance
                    // ... Debug Log ...
                }
                break;

            case "GravityFlip":
                if (!isGravityFlipUnlocked)
                {
                    isGravityFlipUnlocked = true; // SAVES THE STATE
                    player.hasUnlimitedGravity = true; // Sets on the current player
                    // ... Debug Log ...
                }
                break;
        }
    }
}