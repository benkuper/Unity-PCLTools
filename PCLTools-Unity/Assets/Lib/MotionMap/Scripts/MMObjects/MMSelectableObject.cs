using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class MMSelectableObject : MonoBehaviour
{
    [HideInInspector]
    public bool isOver;
    [HideInInspector]
    public bool isSelected;
    [HideInInspector]
    public float selectionProgression;

    public virtual void overChanged(bool isOver) { this.isOver = isOver;  }
    public virtual void selectionChanged(bool isSelected) { this.isSelected = isSelected; }
    public virtual void selectionProgress(float progress) { selectionProgression = progress;  }

    public void deselect()
    {
        selectionChanged(false);
        overChanged(false);
    }

    public void simulateSelection()
    {
        overChanged(true);
        selectionProgression = 0;
        DOTween.To(() => selectionProgression, x => selectionProgression = x, 1, MotionMap.instance.selectionTime).OnUpdate(() => selectionProgress(selectionProgression)).OnComplete(() => selectionChanged(true));
    }
}
