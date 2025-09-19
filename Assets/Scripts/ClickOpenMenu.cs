// Assets/Scripts/UI/ClickOpenMenu.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;           // Mouse / Touchscreen / Keyboard
using UnityEngine.InputSystem.UI;        // InputSystemUIInputModule
#endif

[DisallowMultipleComponent]
public class ClickOpenMenu : MonoBehaviour
{
    [Header("Menu UI")]
    [Tooltip("Root GameObject of the menu canvas (can be disabled by default).")]
    [SerializeField] private GameObject menuCanvasRoot;

    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;

    [Header("Behavior")]
    [Tooltip("Pause the game while menu is open.")]
    [SerializeField] private bool pauseWhileOpen = true;

    [Tooltip("Scene name to load when Quit is pressed.")]
    [SerializeField] private string creditsSceneName = "Credits";

    [Tooltip("Key to toggle the menu (ESC recommended).")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Click Detection")]
    [Tooltip("Camera used for raycasts. If null, uses Camera.main.")]
    [SerializeField] private Camera rayCamera;

    [Tooltip("Layers considered clickable. Leave as Everything to keep it simple.")]
    [SerializeField] private LayerMask interactableLayers = ~0;

    private bool isOpen;
    private Collider cachedCol3D;
    private Collider2D cachedCol2D;

    void Awake()
    {
        cachedCol3D = GetComponent<Collider>();
        cachedCol2D = GetComponent<Collider2D>();

        if (menuCanvasRoot) menuCanvasRoot.SetActive(false);
        EnsureEventSystem();
        WireButtons();
    }

    void Update()
    {
        // --- 1) Keyboard toggle (ESC) works whether menu is open or closed ---
        if (toggleKey != KeyCode.None && IsKeyDown(toggleKey))
        {
            ToggleMenu();
            return; // consume the key this frame
        }

        // --- 2) If already open, no click detection needed ---
        if (isOpen) return;

        // --- 3) Door click/tap opens menu (Input System only) ---
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!TryGetPointerUp(out Vector2 screenPos))
            return;

        var cam = rayCamera ? rayCamera : Camera.main;
        if (!cam) return;

        // 3D raycast
        if (cachedCol3D)
        {
            Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            if (Physics.Raycast(ray, out var hit, float.MaxValue, interactableLayers))
            {
                if (IsSelfOrChild(hit.collider.transform))
                    OpenMenu();
            }
            return;
        }

        // 2D raycast
        if (cachedCol2D)
        {
            Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane));
            var hit = Physics2D.Raycast(wp, Vector2.zero, 0f, interactableLayers);
            if (hit.collider && IsSelfOrChild(hit.collider.transform))
                OpenMenu();
        }
    }

    private bool IsSelfOrChild(Transform t) => t == transform || t.IsChildOf(transform);

    // -------------------- Input System helpers --------------------
    private bool IsKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        switch (key)
        {
            case KeyCode.Escape: return Keyboard.current.escapeKey.wasPressedThisFrame;
            case KeyCode.Space:  return Keyboard.current.spaceKey.wasPressedThisFrame;
            case KeyCode.Return: return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
            default: return false; // add more if needed
        }
#else
        return false;
#endif
    }

    /// <summary>Returns true when a primary pointer was released this frame and outputs its screen position.</summary>
    private bool TryGetPointerUp(out Vector2 screenPos)
    {
        screenPos = default;
#if ENABLE_INPUT_SYSTEM
        // Mouse
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
        }

        // Touch: primary or any touch release
        if (Touchscreen.current != null)
        {
            var primary = Touchscreen.current.primaryTouch;
            if (primary != null && primary.press.wasReleasedThisFrame)
            {
                screenPos = primary.position.ReadValue();
                return true;
            }
            foreach (var t in Touchscreen.current.touches)
            {
                if (t.press.wasReleasedThisFrame)
                {
                    screenPos = t.position.ReadValue();
                    return true;
                }
            }
        }
#endif
        return false;
    }

    // -------------------- Open / Close / Toggle --------------------
    public void OpenMenu()
    {
        if (isOpen) return;
        isOpen = true;

        if (pauseWhileOpen) Time.timeScale = 0f;

        if (menuCanvasRoot)
        {
            menuCanvasRoot.SetActive(true);
            if (resumeButton) EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
        }
        else
        {
            Debug.LogWarning("[ClickOpenMenu] menuCanvasRoot not assigned.");
        }
    }

    public void Resume()
    {
        if (!isOpen) return;
        isOpen = false;

        if (menuCanvasRoot) menuCanvasRoot.SetActive(false);
        if (pauseWhileOpen) Time.timeScale = 1f;
    }

    public void ToggleMenu()
    {
        if (isOpen) Resume();
        else OpenMenu();
    }

    // Optional: hook this to an Input Action (Perform) if you prefer action maps over KeyCode
    public void ToggleMenuFromInput()
    {
        ToggleMenu();
    }

    public void QuitToCredits()
    {
        if (pauseWhileOpen) Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(creditsSceneName))
            SceneManager.LoadScene(creditsSceneName);
        else
            Debug.LogWarning("[ClickOpenMenu] creditsSceneName is empty.");
    }

    // -------------------- Wiring / EventSystem --------------------
    private void WireButtons()
    {
        if (resumeButton)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(Resume);
        }
        if (quitButton)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitToCredits);
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        DontDestroyOnLoad(go);
    }

    void OnDisable()
    {
        // Safety: if object is disabled while menu open, restore timeScale
        if (isOpen && pauseWhileOpen) Time.timeScale = 1f;
    }
}
