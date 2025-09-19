using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneRedirector : MonoBehaviour
{
    [Header("Target Scene")]
    [Tooltip("The exact name of the scene you want to load.")]
    [SerializeField] private string targetSceneName = "YourSceneNameHere";

    [Header("Optional Delay")]
    [Tooltip("Time in seconds before the target scene loads.")]
    [SerializeField] private float delay = 0f;

    private void Start()
    {
        if (delay <= 0f)
        {
            LoadTargetScene();
        }
        else
        {
            Invoke(nameof(LoadTargetScene), delay);
        }
    }

    private void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogError("SceneRedirector: Target scene name is not set!");
        }
    }
}
