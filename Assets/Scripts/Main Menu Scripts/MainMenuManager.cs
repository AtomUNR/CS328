//using Unity.VisualScripting;
using UnityEngine;
//using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject firstSelectedButton;

    public void Start()
    {
       // Debug.Log("START BUTTON PRESSED! \n");
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    public void StartGame()
    {
        Debug.Log( "START BUTTON PRESSED \n" );
        SceneManager.LoadScene("Level Select");
    }

    public void QuitGame()
    {
        Debug.Log("QUIT BUTTON PRESSED \n");
        Application.Quit();
    }
}
