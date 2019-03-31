using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BK.PCL;

public class ClusterManagerDemo : MonoBehaviour
{
    public ClusterManager clusterManager;

    public GameObject clusterPrefab;
    Dictionary<int, GameObject> clusterIdMap;

    void Start()
    {
        clusterManager.clusterAdded += clusterAdded;
        clusterManager.clusterUpdated += clusterUpdated;
        clusterManager.clusterGhosted += clusterGhosted;
        clusterManager.clusterRemoved += clusterRemoved;

        clusterIdMap = new Dictionary<int, GameObject>();
    }

    void clusterAdded(Cluster cluster)
    {
        if(clusterIdMap.ContainsKey(cluster.id))
        {
            Debug.LogWarning("There should not be a cluster with id "+cluster.id+" here !");
            return;
        }

        GameObject go = Instantiate(clusterPrefab);
        go.transform.SetParent(transform);
        go.transform.position = cluster.center;
        go.GetComponent<TextMesh>().text = cluster.id.ToString();
        go.GetComponent<TextMesh>().color = cluster.color;

        clusterIdMap.Add(cluster.id, go);
    }

    void clusterUpdated(Cluster cluster)
    {
        if (!clusterIdMap.ContainsKey(cluster.id))
        {
            Debug.LogWarning("There should  be a cluster with id " + cluster.id + " here !");
            return;
        }

        clusterIdMap[cluster.id].transform.position = cluster.center;
        clusterIdMap[cluster.id].GetComponent<TextMesh>().color = cluster.color;
    }

    void clusterGhosted(Cluster cluster)
    {
        clusterIdMap[cluster.id].GetComponent<TextMesh>().color = cluster.color;
    }

    void clusterRemoved(Cluster cluster)
    {
        if (!clusterIdMap.ContainsKey(cluster.id))
        {
            Debug.LogWarning("There should be a cluster with id " + cluster.id + " here !");
            return;
        }

        Destroy(clusterIdMap[cluster.id]);
        clusterIdMap.Remove(cluster.id);
    }

    void Update()
    {
        
    }
}
