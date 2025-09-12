// MortarPestleUIMinigame.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MortarPestleUIMinigame : UIMinigameBase
{
    [Header("Refs")]
    public Button potionBtn;
    public Image potImage;
    public UIDragItem pestleDrag;
    public UIDropZone mortarZone;
    public UIDropZone offTableZone;
    public TMP_Text statusText;

    [Header("Sprites")]
    public Sprite potEmpty;
    public Sprite potPoured;
    public Sprite pestleClean;
    public Sprite pestleGround; // shown after grinding

    [Header("Output")]
    public ItemSO outputItem;   // e.g., Mashed Plant Potion

    [Header("Timings")]
    public float pourTime = 0.6f;
    public float grindTime = 0.8f;

    private bool hasPoured = false;
    private bool hasGround = false;

    public override void StartMinigame(System.Action<ItemSO> onComplete)
    {
        base.StartMinigame(onComplete);

        hasPoured = false;
        hasGround = false;
        if (potImage) potImage.sprite = potEmpty;
        if (pestleDrag && pestleDrag.Rect && pestleClean) pestleDrag.GetComponent<Image>().sprite = pestleClean;
        if (statusText) statusText.text = "Click the growth potion.";

        potionBtn.onClick.RemoveAllListeners();
        potionBtn.onClick.AddListener(() => StartCoroutine(PourRoutine()));

        // hook zone drops
        var mortarHook = mortarZone as UIDropZoneHook;
        var offHook = offTableZone as UIDropZoneHook;
        if (!mortarHook) mortarHook = mortarZone.gameObject.AddComponent<UIDropZoneHook>();
        if (!offHook) offHook = offTableZone.gameObject.AddComponent<UIDropZoneHook>();

        mortarHook.onDropped = OnDroppedIntoMortar;
        offHook.onDropped = OnDroppedOffTable;
    }

    private IEnumerator PourRoutine()
    {
        if (hasPoured) yield break;
        potionBtn.interactable = false;

        // simple "pour" animation: quickly change pot sprite
        float t = 0f;
        while (t < pourTime)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (potImage && potPoured) potImage.sprite = potPoured;
        hasPoured = true;
        if (statusText) statusText.text = "Drag the pestle into the mortar.";
    }

    private void OnDroppedIntoMortar(GameObject draggedGO)
    {
        if (!hasPoured) { if (statusText) statusText.text = "Add the potion first."; return; }
        if (hasGround) return;

        var img = draggedGO.GetComponent<Image>();
        StartCoroutine(GrindRoutine(img));
    }

    private IEnumerator GrindRoutine(Image pestleImg)
    {
        // fake grind: wait, then swap sprite
        float t = 0f;
        if (statusText) statusText.text = "Grinding...";
        while (t < grindTime) { t += Time.deltaTime; yield return null; }
        if (pestleImg && pestleGround) pestleImg.sprite = pestleGround;

        hasGround = true;
        if (statusText) statusText.text = "Drag the pestle off the table.";
    }

    private void OnDroppedOffTable(GameObject draggedGO)
    {
        if (!hasGround) { if (statusText) statusText.text = "Grind it first."; return; }

        // success — return output item
        onDone?.Invoke(outputItem);
        onDone = null;
    }
}

// helper to expose a callback from UIDropZone
public class UIDropZoneHook : UIDropZone
{
    public System.Action<GameObject> onDropped;
    public override void OnDrop(UnityEngine.EventSystems.PointerEventData eventData)
    {
        onDropped?.Invoke(eventData.pointerDrag);
    }
}
