using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BK.PCL;
using UnityEditor.IMGUI.Controls;

[CustomEditor(typeof(ClusterManager)), CanEditMultipleObjects]
public class ClusterManagerEditor : Editor
{
    private BoxBoundsHandle boundsHandle = new BoxBoundsHandle();

    protected virtual void OnSceneGUI()
    {
        ClusterManager cm = target as ClusterManager;

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


       // EditorGUI.BeginChangeCheck();
        Quaternion r = Handles.RotationHandle(cm.transform.parent.localRotation, Vector3.zero);
        //if (EditorGUI.EndChangeCheck())
        //{
            //Undo.RecordObject(cm.transform.parent, "Rotate Kinect");
            cm.transform.parent.localRotation = r;
        //}
    }
}
