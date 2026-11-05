using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectManager : MonoBehaviour
{
    public void LoadFloor1()
    {
        SceneManager.LoadScene("First Level");
    }

    public void LoadFloor2()
    {
        Debug.Log("Floor 2 is not available yet.");
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }
}