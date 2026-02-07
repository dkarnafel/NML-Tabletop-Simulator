using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadDeckbuilder()
    {
        SceneManager.LoadScene("Deck Builder");
    }
    public void LoadMainMenu()
    {
        SceneManager.LoadScene("Main menu");
    }
    public void LoadDeckSelector()
    {
        SceneManager.LoadScene("Deck Selection");
    }
}
