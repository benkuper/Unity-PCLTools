using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BK.PCL
{
    public class ClusterObject : MonoBehaviour
    {

        Cluster cluster;
        Vector3 centerVelocity = Vector3.zero;

        public  Vector3 aimOrigin = Vector3.zero;
        public Vector3 aimDirection = Vector3.zero;
        Vector3 aimOriginVelocity = Vector3.zero;
        Vector3 aimDirectionVelocity = Vector3.zero;

        public float smoothing;

        public bool drawDebug;

        virtual public void Update()
        {
            if (cluster == null) return;

            transform.position = Vector3.SmoothDamp(transform.position, cluster.center, ref centerVelocity, smoothing);
            aimOrigin = Vector3.SmoothDamp(aimOrigin, cluster.ray.origin, ref aimOriginVelocity, smoothing);
            aimDirection = Vector3.SmoothDamp(aimDirection, cluster.ray.direction, ref aimDirectionVelocity, smoothing);

            //Debug.DrawRay(cluster.ray.origin, cluster.ray.direction * 10, Color.grey);
            if(drawDebug) Debug.DrawRay(aimOrigin, aimDirection * 10, cluster.color);
        }


        virtual public void updateData(Cluster c, bool directSet = false)
        {
            cluster = c;

            if (directSet)
            {
                transform.position = cluster.center;
                aimOrigin = cluster.ray.origin;
                aimDirection = cluster.ray.direction;
            }
        }

        public void OnDrawGizmos()
        {
            if (cluster == null) return;
            if (!drawDebug) return;
            Gizmos.color = cluster.color;
            Gizmos.DrawWireSphere(aimOrigin, .01f);
            Gizmos.DrawWireCube(aimOrigin + aimDirection, Vector3.one * .01f);
            Gizmos.DrawLine(aimOrigin, aimOrigin + aimDirection);
        }
    }

}
