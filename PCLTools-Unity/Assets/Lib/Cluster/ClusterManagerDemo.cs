using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BK.PCL;

public class ClusterManagerDemo : MonoBehaviour
{
    public ClusterManager clusterManager;

    public GameObject clusterPrefab;
    Dictionary<int, ClusterViz> clusterIdMap;

    void Start()
    {
        clusterManager.clusterAdded += clusterAdded;
        clusterManager.clusterUpdated += clusterUpdated;
        clusterManager.clusterGhosted += clusterGhosted;
        clusterManager.clusterRemoved += clusterRemoved;

        clusterIdMap = new Dictionary<int, ClusterViz>();
    }

    void clusterAdded(Cluster cluster)
    {
        if(clusterIdMap.ContainsKey(cluster.id))
        {
            Debug.LogWarning("There should not be a cluster with id "+cluster.id+" here !");
            return;
        }

        ClusterViz c = Instantiate(clusterPrefab).GetComponent<ClusterViz>();
        c.transform.SetParent(transform);

        c.updateData(cluster, true);
        clusterIdMap.Add(cluster.id, c);
    }

    void clusterUpdated(Cluster cluster)
    {
        if (!clusterIdMap.ContainsKey(cluster.id))
        {
            Debug.LogWarning("There should  be a cluster with id " + cluster.id + " here !");
            return;
        }

        clusterIdMap[cluster.id].updateData(cluster, false);
    }

    void clusterGhosted(Cluster cluster)
    {
        clusterIdMap[cluster.id].updateData(cluster, false);
    }

    void clusterRemoved(Cluster cluster)
    {
        if (!clusterIdMap.ContainsKey(cluster.id))
        {
            Debug.LogWarning("There should be a cluster with id " + cluster.id + " here !");
            return;
        }

        Destroy(clusterIdMap[cluster.id].gameObject);
        clusterIdMap.Remove(cluster.id);
    }

    void Update()
    {
        
    }
}
