using UnityEngine;
using UnityEngine.SceneManagement;

namespace JoaoMilone.Demo
{
    public class EscapeListener : MonoBehaviour
    {
        [SerializeField]
        private string sceneToLoad = "Menu";

        private void EscapePressed() 
        {
            if (SceneManager.GetActiveScene().name != "Menu")
                SceneManager.LoadScene(sceneToLoad);
            else
                Application.Quit();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                EscapePressed();
        }
    }
}
