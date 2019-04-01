using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class MMOChangeMat : MMSelectableObject
{
    List<Material> mats;
    List<Material> materials;
    List<Color> initMaterialColors; //keep init color to revert
    public Color overColor = Color.yellow;
    public Color selectionColor = Color.green;
    public bool affectChildrens;

    void Start()
    {
        materials = new List<Material>();
        initMaterialColors = new List<Color>();

        Renderer[] renderers = affectChildrens ? GetComponentsInChildren<Renderer>() : GetComponents<Renderer>();

        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                materials.Add(m);
                initMaterialColors.Add(m.GetColor("_Color"));
            }
        }
    }

    void Update()
    {
        
    }

    public override void overChanged(bool isOver)
    {
        base.overChanged(isOver);

        if (!isSelected)
        {
            if (isOver) setAllMaterialsColors(overColor);
            else resetAllMaterialsColors();
        }
    }

    public override void selectionChanged(bool isSelected)
    {
        base.selectionChanged(isSelected);

        if (isSelected)  setAllMaterialsColors(selectionColor);
        else resetAllMaterialsColors();
    }

    public override void selectionProgress(float progress)
    {
        base.selectionProgress(progress);
    }

    void setAllMaterialsColors(Color targetColor, float time = .5f)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Material m = materials[i];
            m.DOColor(targetColor, "_Color", time);
        }
    }

    void resetAllMaterialsColors(float time = .5f)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Material m = materials[i];
            Color c = initMaterialColors[i];
            m.DOColor(c, "_Color",time);
        }
    }
}
