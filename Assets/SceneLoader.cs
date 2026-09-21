using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void OpenPhysicsScene()
    {
        SceneManager.LoadScene("PhysicsScene");
    }
}