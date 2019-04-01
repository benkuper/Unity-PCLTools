using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MMSelectableObject),true)]
public class MMSelectableObjectEditor : Editor
{
    MMSelectableObject mmo;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (mmo == null) mmo = target as MMSelectableObject;

        if(Application.isPlaying)
        {
            if (GUILayout.Button("Simulate selection")) mmo.simulateSelection();
            if (GUILayout.Button("Deselect")) mmo.deselect();
        }
        
    }
}
