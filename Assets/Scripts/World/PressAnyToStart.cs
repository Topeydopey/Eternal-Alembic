// Assets/Scripts/UI/PressAnyToStart.cs
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Linq;
#endif

[DisallowMultipleComponent]
public class PressAnyToStart : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private float acceptInputAfterSeconds = 0.25f;
    [SerializeField] private bool fadeOutOnPress = true;
    [SerializeField] private float fadeOutSeconds = 0.6f;
    [SerializeField] private string sceneNameToLoad = "Home";

    [Header("Run Reset")]
    [SerializeField] private bool resetPerRunOnStart = true;

    [Header("Events")]
    public UnityEvent onPressed;

    private bool _armed;
    private bool _fired;

    private void OnEnable()
    {
        _armed = false; _fired = false;
        Invoke(nameof(ArmInput), acceptInputAfterSeconds);
    }
    private void ArmInput() => _armed = true;

    private void Update()
    {
        if (_fired || !_armed) return;
        if (WasAnyPressThisFrame())
        {
            _fired = true;
            onPressed?.Invoke();
            StartFlow();
        }
    }

    private void StartFlow()
    {
        if (fadeOutOnPress)
        {
            var fader = ScreenFader.CreateDefault();
            fader.FadeOut(fadeOutSeconds);
            StartCoroutine(LoadAfterDelay(fadeOutSeconds));
        }
        else
        {
            StartCoroutine(LoadAfterDelay(0f));
        }
    }

    private System.Collections.IEnumerator LoadAfterDelay(float t)
    {
        if (t > 0f) yield return new WaitForSeconds(t);

        // 🔑 Clear per-run state BEFORE loading gameplay
        if (resetPerRunOnStart)
            GameRunReset.ResetNowForNewRun();

        if (!string.IsNullOrWhiteSpace(sceneNameToLoad))
        {
            var op = SceneManager.LoadSceneAsync(sceneNameToLoad);
            while (!op.isDone) yield return null;
        }
    }

    private bool WasAnyPressThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current?.anyKey.wasPressedThisFrame == true) return true;
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame)) return true;
        if (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true) return true;

        var pads = Gamepad.all;
        for (int i = 0; i < pads.Count; i++)
        {
            var gp = pads[i];
            if (gp == null) continue;
            if (gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame ||
                gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame ||
                gp.buttonNorth.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame ||
                gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame ||
                gp.leftStickButton.wasPressedThisFrame || gp.rightStickButton.wasPressedThisFrame ||
                gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
                gp.dpad.left.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame)
                return true;

            if (gp.allControls.Any(c => c is ButtonControl bc && bc.wasPressedThisFrame))
                return true;
        }
        return false;
#else
        return Input.anyKeyDown ||
               Input.GetMouseButtonDown(0) ||
               Input.GetMouseButtonDown(1) ||
               Input.GetMouseButtonDown(2);
#endif
    }
}
