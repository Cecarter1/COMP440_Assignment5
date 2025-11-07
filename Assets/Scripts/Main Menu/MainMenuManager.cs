using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject howToPlayPanel;

    [Header("Scene Settings")]
    [Tooltip("Name of the first level or tutorial scene to load when Play is pressed.")]
    public string firstSceneName = "Tutorial";  // change to your first level scene name

    // Called by the Play button
    public void PlayGame()
    {
        if (!string.IsNullOrEmpty(firstSceneName))
        {
            SceneManager.LoadScene(firstSceneName);
        }
        else
        {
            Debug.LogError("MainMenuManager: firstSceneName is empty!");
        }
    }

    // Called by the Quit button
    public void QuitGame()
    {
        Debug.Log("MainMenuManager: QuitGame called.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;  // stops Play mode in editor
#else
        Application.Quit();  // quits the built game
#endif
    }

    // Called by the How To Play button
    public void OpenHowToPlay()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(true);
    }

    // Called by the Back button on How To Play panel
    public void CloseHowToPlay()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);
    }
}
