// UIMinigameBase.cs
using System;
using UnityEngine;

public abstract class UIMinigameBase : MonoBehaviour
{
    protected Action<ItemSO> onDone;

    public virtual void StartMinigame(Action<ItemSO> onComplete)
    {
        onDone = onComplete;
        gameObject.SetActive(true);
    }
}
