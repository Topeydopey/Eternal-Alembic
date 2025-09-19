using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

public class BuildRenderDoctor : MonoBehaviour
{
    [Header("Run-once fixer")]
    public bool applyFixes = true;

    void Start()
    {
        Debug.Log($"[Doctor] Scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");
        // Quality sanity (builds can start at a different level)
        Debug.Log($"[Doctor] QualityLevel: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");

        var cams = Camera.allCameras;
        Debug.Log($"[Doctor] Cameras: {cams.Length}");
        foreach (var c in cams)
        {
            string info = $"Cam '{c.name}' enabled={c.enabled} type={c.cameraType} " +
                          $"display={c.targetDisplay} rect={c.rect} clear={c.clearFlags} " +
                          $"cullMask={c.cullingMask} tgtRT={(c.targetTexture ? c.targetTexture.name : "null")}";

#if UNITY_RENDER_PIPELINE_UNIVERSAL
            var add = c.GetUniversalAdditionalCameraData();
            if (add)
                info += $" | URP RenderType={add.renderType} Stack={add.cameraStack.Count} RendererIdx={add.scriptableRendererIndex}";
            else
                info += " | URP:N/A";
#endif
            Debug.Log("[Doctor] " + info);
        }

        var cam = Camera.main;
        if (!cam) { Debug.LogError("[Doctor] No Camera tagged MainCamera."); return; }

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        var addData = cam.GetUniversalAdditionalCameraData();
#endif

        if (applyFixes)
        {
            // 1) Always render to screen, Display 1, full viewport
            cam.enabled = true;
            cam.targetDisplay = 0;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            if (cam.targetTexture != null)
            {
                Debug.LogWarning("[Doctor] Clearing Main Camera TargetTexture.");
                cam.targetTexture = null;
            }
            // 2) Culling mask: Everything (temporarily)
            cam.cullingMask = ~0;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
            if (addData)
            {
                // 3) Force Base camera and clear overlays
                if (addData.renderType != CameraRenderType.Base)
                {
                    Debug.LogWarning("[Doctor] Main Camera was Overlay. Forcing Base.");
                    addData.renderType = CameraRenderType.Base;
                }
                if (addData.cameraStack.Count > 0)
                {
                    Debug.LogWarning("[Doctor] Clearing camera stack.");
                    addData.cameraStack.Clear();
                }
                // 4) Default renderer index (assumes your URP asset default is the 2D renderer)
                addData.SetRenderer(0);

                // 5) Depth texture ON if any effect needs depth
                addData.requiresDepthTexture = true;
            }
            else
            {
                Debug.LogWarning("[Doctor] No UniversalAdditionalCameraData on Main Camera.");
            }
#endif
            // 6) Remove unsafe command buffers from non-Base / UI cameras
            foreach (var c in cams)
            {
                foreach (CameraEvent evt in System.Enum.GetValues(typeof(CameraEvent)))
                {
                    var bufs = c.GetCommandBuffers(evt);
                    foreach (var b in bufs)
                    {
                        // Be conservative: strip all CBs from non-Base or UI cameras at runtime
#if UNITY_RENDER_PIPELINE_UNIVERSAL
                        var a = c.GetUniversalAdditionalCameraData();
                        bool isOverlay = a && a.renderType == CameraRenderType.Overlay;
#else
                        bool isOverlay = false;
#endif
                        if (isOverlay || c.cameraType == CameraType.SceneView || c.cameraType == CameraType.Game)
                        {
                            c.RemoveCommandBuffer(evt, b);
                            Debug.LogWarning($"[Doctor] Removed CB '{b.name}' from {c.name} at {evt}.");
                        }
                    }
                }
            }

            Debug.Log("[Doctor] Fixes applied. If you now see the world, re-introduce stacks/effects step-by-step.");
        }

        // Duplicate MainCamera check
        int mains = 0;
        foreach (var c in cams) if (c.CompareTag("MainCamera")) mains++;
        if (mains > 1) Debug.LogWarning($"[Doctor] {mains} cameras tagged MainCamera (duplicate from bootstrap?).");
    }
}
