using UnityEngine;
using System.Collections.Generic;
using System;
using DG.Tweening;
using BK.PCL;
using UnityOSC;

public class MotionMap : ClusterManagerClient
{
    public static MotionMap instance;

    [Header("Setup")]
    public MotionMapCursor cursorPrefab;

    [Header("Zone Selection")]
    public bool autoDeselectOnNewSelection;
    public float selectionTime = 2;
    public float progressionDecayTime = 1;

    [Header("Demo mode")]
    public float demoModeTime = 30;
    bool demoMode;

    int zonesLayer;
    int plateauLayer;

    MotionMapZone[] zones;
    MotionMapZone selectedZone;


    Dictionary<ClusterObject, MotionMapCursor> clusterCursorMap;

    public override void Awake()
    {
        base.Awake();

        instance = this;


        zonesLayer = LayerMask.GetMask(new string[] { "zones" });
        plateauLayer = LayerMask.GetMask(new string[] { "plateau" });

        zones = FindObjectsOfType<MotionMapZone>();

        clusterCursorMap = new Dictionary<ClusterObject, MotionMapCursor>();
    }

    void Update () {
        foreach (MotionMapZone z in zones) z.isOverInThisFrame = false;

        foreach(var clusterKeyVal in clusterIdMap)
        {
            ClusterObject cluster = clusterKeyVal.Value;

            Vector3 targetPos = Vector3.zero;
            Vector3 targetRot = Vector3.up;

            RaycastHit hit;
            Ray cursorRay = new Ray(cluster.aimOrigin, cluster.aimDirection);
            if (Physics.Raycast(cursorRay, out hit, 100, zonesLayer))
            {
                MotionMapZone z = hit.collider.GetComponent<MotionMapZone>();
                if(z != null)  z.isOverInThisFrame = true;

                targetPos = new Vector3(hit.transform.position.x, 0.01f, hit.transform.position.z);
                targetRot = Vector3.up;
            }
            else if (Physics.Raycast(cursorRay, out hit, 100.0f, plateauLayer))
            {
                targetPos = hit.point + hit.normal * 0.01f;
                targetRot = hit.normal;
            }

            MotionMapCursor cursor = getCursorForCluster(cluster);

            if(hit.transform == null)
            {
                cursor.gameObject.SetActive(false);
            }else
            {
                cursor.gameObject.SetActive(true);
                cursor.transform.position = targetPos;
                cursor.transform.rotation = Quaternion.Euler(targetRot);
            }
        }

        foreach (MotionMapZone z in zones)
        {
            z.setOver(z.isOverInThisFrame);
            if(z.over)
            {
                float curSelectTime = (Time.time - z.overStartTime) / selectionTime;
                z.setSelectionProgression(curSelectTime);
                if(curSelectTime >= 1)
                {
                    setSelectedZone(z);
                }
            }else
            {
                z.setSelectionProgression(z.selectionProgression - Time.deltaTime / progressionDecayTime);
            }
        }

        //if (Input.GetKeyDown(KeyCode.C)) canvas.SetActive(!canvas.activeInHierarchy);
    }

    public override void clusterAdded(Cluster cluster)
    {
        base.clusterAdded(cluster);

        MotionMapCursor cursor = Instantiate(cursorPrefab.gameObject).GetComponent<MotionMapCursor>();
        cursor.transform.parent = transform.Find("Cursors");
        cursor.gameObject.SetActive(false);
        cursor.setColor(cluster.color);
        clusterCursorMap.Add(clusterIdMap[cluster.id], cursor);

        CancelInvoke("startDemo");
        if (demoMode) stopDemo();
    }

    public override void clusterRemoved(Cluster cluster)
    {
        MotionMapCursor cursor = getCursorForCluster(clusterIdMap[cluster.id]);

        clusterCursorMap.Remove(clusterIdMap[cluster.id]);

        if (cursor != null) Destroy(cursor.gameObject);

        base.clusterRemoved(cluster);
        if(clusterIdMap.Count == 0) Invoke("startDemo", demoModeTime);
    }

    public void setSelectedZone(MotionMapZone zone)
    {
        if (zone == selectedZone && zone.selected) return;

        if (selectedZone != null)
        {
            if(autoDeselectOnNewSelection) selectedZone.setSelected(false);
        }

        selectedZone = zone;

        if(selectedZone != null)
        {
            selectedZone.setSelected(true);
        }

        OSCMessage m = new OSCMessage("/lastSelectedZone");
        m.Append(selectedZone.id);
        OSCMaster.sendMessage(m);
    }


    MotionMapCursor getCursorForCluster(ClusterObject c)
    {
        return clusterCursorMap.ContainsKey(c) ? clusterCursorMap[c] : null;
    }


    public void startDemo()
    {
        demoMode = true;
    }

    public void stopDemo()
    {
        demoMode = false;
    }
    
}
