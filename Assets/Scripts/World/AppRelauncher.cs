// Assets/Scripts/Utility/AppRelauncher.cs
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public static class AppRelauncher
{
    /// <summary>Start a new instance of the game, then quit this one. Optionally fade out first (uses ScreenFader).</summary>
    public static void Relaunch(float fadeOutSeconds = 0.4f)
    {
        var host = new GameObject("~AppRelauncher");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<_Runner>().Begin(fadeOutSeconds);
    }

    private sealed class _Runner : MonoBehaviour
    {
        private float fadeOutSeconds;

        public void Begin(float fade)
        {
            fadeOutSeconds = Mathf.Max(0f, fade);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var fader = ScreenFader.CreateDefault();
            if (fadeOutSeconds > 0f) yield return fader.FadeOut(fadeOutSeconds);
            else fader.SetAlpha(1f); // ensure black

            LaunchNewInstance();
            Application.Quit();
        }

        private void LaunchNewInstance()
        {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            };
            Process.Start(psi);
#elif UNITY_STANDALONE_OSX
            // Application.dataPath = <App>.app/Contents/Resources/Data -> go up to .app
            string appBundle = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            Process.Start("open", $"-n \"{appBundle}\"");
#elif UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayDialog(
                "Restart Game",
                "Hard restart isn't supported in the Editor. Stop Play and press Play again.",
                "OK");
#else
            Debug.LogWarning("[AppRelauncher] Relaunch not supported on this platform.");
#endif
        }
    }
}
