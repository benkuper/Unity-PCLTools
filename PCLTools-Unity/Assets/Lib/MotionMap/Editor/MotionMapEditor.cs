using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using BK.PCL;

[CustomEditor(typeof(MotionMap)), CanEditMultipleObjects]
public class MotionMapEditor : Editor
{

    private BoxBoundsHandle boundsHandle = new BoxBoundsHandle();

    protected virtual void OnSceneGUI()
    {
        ClusterManager cm = (target as MotionMap).clusterManager;

        if (cm == null) return;

        boundsHandle.handleColor = Color.yellow;
        boundsHandle.wireframeColor = Color.yellow;
        boundsHandle.center = cm.mainBounds.center;
        boundsHandle.size = cm.mainBounds.size;

        EditorGUI.BeginChangeCheck();
        boundsHandle.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cm, "Change Main Bounds");
            cm.mainBounds = new Bounds(boundsHandle.center, boundsHandle.size);
        }

        boundsHandle.handleColor = Color.red;
        boundsHandle.wireframeColor = Color.red;
        for (int i = 0; i < cm.excludeBounds.Count; i++)
        {

            boundsHandle.center = cm.excludeBounds[i].center;
            boundsHandle.size = cm.excludeBounds[i].size;

            EditorGUI.BeginChangeCheck();
            boundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cm, "Change Exclude Bounds");
                cm.excludeBounds[i] = new Bounds(boundsHandle.center, boundsHandle.size);
            }
        }

    }
}
