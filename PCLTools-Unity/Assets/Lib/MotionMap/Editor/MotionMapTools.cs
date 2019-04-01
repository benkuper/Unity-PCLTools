using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MotionMapTools
{
    // Add a new menu item under an existing menu

    [MenuItem("Motion Map/Add Interaction Zone")]
    private static void AddInteractionZone()
    {
        GameObject zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lib/MotionMap/Prefabs/Zone.prefab");
        if(zonePrefab == null)
        {
            Debug.LogWarning("Zone prefab was not found");
            return;
        }
        GameObject zone = (GameObject)PrefabUtility.InstantiatePrefab(zonePrefab);

        GameObject container = GameObject.Find("MotionMapRig/InteractiveZones");
        //GameObject sceneContainer = GameObject.Find("MotionMapRig/Scene");

        if (container != null)
        {
            zone.transform.parent = container.transform;
            zone.layer = container.layer;

            MotionMapZone z = zone.GetComponent<MotionMapZone>();

            GameObject[] objects = Selection.gameObjects;
            List<GameObject> objectsToAdd = new List<GameObject>();

            Bounds bounds = new Bounds();
            z.objects = new GameObject[objects.Length];

            for(int i=0;i<objects.Length;i++)
            {
                z.objects[i] = objects[i];
                if (bounds.size == Vector3.zero) bounds = GetMaxBounds(objects[i]);
                else bounds.Encapsulate(GetMaxBounds(objects[i]));
            }

          
            z.transform.position = bounds.center;
            z.transform.localScale = bounds.size * 1.2f;

        }else
        {
            Debug.Log("InteractiveZones container not found");
        }

        Selection.activeObject = zone;
        Undo.RegisterCreatedObjectUndo(zone, "Create Interactive Zone");
    }


    public static Bounds GetMaxBounds(GameObject g)
    {
        //Debug.Log("Get max bounds for " + g.name);
        var b = new Bounds(g.transform.position, Vector3.zero);
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
        {
            b.Encapsulate(r.bounds);
        }
        return b;
    }
}