using UnityEngine;

namespace BK.PCL
{
    public class Cluster
    {
        public static int idCounter = 0;

        public int[] indices;
        public int id;
        public int numPoints;
        public Vector3 center;
        public Vector3 orientation;
        public Bounds bounds;
        public Color color;

        public float timeAtGhosted;

        public Cluster(int[] indices, int clusterID, Vector3 center, Bounds bounds)
        {
            this.id = clusterID;
            this.indices = indices;
            numPoints = indices.Length;
            this.center = center;
            this.bounds = bounds;
            this.timeAtGhosted = -1;
        }

        public bool isGhost()
        {
            return timeAtGhosted > -1;
        }

    }
}
