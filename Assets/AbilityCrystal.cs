using UnityEngine;

public class AbilityCrystal : MonoBehaviour
{
    [Tooltip("The name of the ability to unlock (e.g., Dash, DoubleJump)")]
    public string abilityToUnlock;

    [Tooltip("Check this if the crystal should be disabled after being collected.")]
    public bool disableAfterCollection = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Check for the player tag
        if (other.CompareTag("Player"))
        {
            // 2. Tell the persistent manager to unlock the ability
            AbilityManager.Instance?.UnlockAbility(abilityToUnlock);

            // 3. Remove the item from the level
            if (disableAfterCollection)
            {
                // Optional: Play a sound/particle effect here
                gameObject.SetActive(false); // Disable it immediately
                // Alternatively: Destroy(gameObject);
            }
        }
    }
}