using UnityEngine;

namespace BK.PCL
{
    public class Cluster
    {
        public static int idCounter = 0;

        public Vector3[] points;
        public Vector3[] border1Points;
        public Vector3[] border2Points;
        public int id;
        public int numPoints;
        public Vector3 center;
        public Ray ray;
        public Bounds bounds;
        public Color color;

        public float timeAtGhosted;

        public Cluster(Vector3[] points, int clusterID, Vector3 center, Bounds bounds)
        {
            this.id = clusterID;
            this.points = points;
            numPoints = points.Length;
            this.center = center;
            this.bounds = bounds;
            this.timeAtGhosted = -1;
            border1Points = new Vector3[0];
            border2Points = new Vector3[0];
        }


        public bool isGhost()
        {
            return timeAtGhosted > -1;
        }

    }
}
