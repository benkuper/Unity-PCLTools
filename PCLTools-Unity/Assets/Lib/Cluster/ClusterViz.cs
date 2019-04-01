using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BK.PCL
{
    public class ClusterViz : ClusterObject
    {
        TextMesh tm;
       

        void Awake()
        {
            tm = GetComponent<TextMesh>();
        }


        override public void updateData(Cluster c, bool directSet = false)
        {
            base.updateData(c, directSet);

            tm.text = c.id.ToString();
            tm.color = c.color;
        }

        

    }
}