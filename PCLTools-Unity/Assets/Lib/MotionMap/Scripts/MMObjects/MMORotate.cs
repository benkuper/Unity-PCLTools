using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class MMORotate : MMSelectableObject
{
    Quaternion initRotation;

    public Vector3 overSpeed;
    public Vector3 selectionSpeed;
    public AnimationCurve overEvolution;

    void Start()
    {
        initRotation = transform.localRotation;
    }

    void Update()
    {
        if (isSelected) transform.Rotate(selectionSpeed*360*Time.deltaTime);
        else if (isOver) transform.Rotate(overEvolution.Evaluate(selectionProgression) * overSpeed * 360 * Time.deltaTime);
    }

    public override void overChanged(bool isOver)
    {
        base.overChanged(isOver);
        if (!isOver && !isSelected) goHome(); 
    }

    public override void selectionChanged(bool isSelected)
    {
        base.selectionChanged(isSelected);
        if (!isSelected) goHome();
    }

    public void goHome()
    {
        transform.DOLocalRotate(initRotation.eulerAngles, 1);    
    }
}
