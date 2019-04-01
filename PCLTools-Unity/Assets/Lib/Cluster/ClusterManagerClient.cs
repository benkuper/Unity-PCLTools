using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BK.PCL
{
    public class ClusterManagerClient : MonoBehaviour
    {
        public ClusterManager clusterManager;

        public ClusterObject clusterPrefab;
        protected Dictionary<int, ClusterObject> clusterIdMap;

        virtual public void Awake()
        {
            clusterManager.clusterAdded += clusterAdded;
            clusterManager.clusterUpdated += clusterUpdated;
            clusterManager.clusterGhosted += clusterGhosted;
            clusterManager.clusterRemoved += clusterRemoved;

            clusterIdMap = new Dictionary<int, ClusterObject>();
        }

        virtual public void clusterAdded(Cluster cluster)
        {
            if (clusterIdMap.ContainsKey(cluster.id))
            {
                Debug.LogWarning("There should not be a cluster with id " + cluster.id + " here !");
                return;
            }

            ClusterObject c = Instantiate(clusterPrefab.gameObject).GetComponent<ClusterObject>();
            c.transform.SetParent(transform);

            c.updateData(cluster, true);
            clusterIdMap.Add(cluster.id, c);
        }

        virtual public void clusterUpdated(Cluster cluster)
        {
            if (!clusterIdMap.ContainsKey(cluster.id))
            {
                Debug.LogWarning("There should  be a cluster with id " + cluster.id + " here !");
                return;
            }

            clusterIdMap[cluster.id].updateData(cluster, false);
        }

        virtual public void clusterGhosted(Cluster cluster)
        {
            clusterIdMap[cluster.id].updateData(cluster, false);
        }

        virtual public void clusterRemoved(Cluster cluster)
        {
            if (!clusterIdMap.ContainsKey(cluster.id))
            {
                Debug.LogWarning("There should be a cluster with id " + cluster.id + " here !");
                return;
            }

            Destroy(clusterIdMap[cluster.id].gameObject);
            clusterIdMap.Remove(cluster.id);
        }
    }
}