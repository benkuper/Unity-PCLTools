using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BK.Kinect
{
    public class KinectTextureToMat : MonoBehaviour
    {
        public KinectManager kinect;
        
        void Start()
        {
            GetComponent<Renderer>().material.mainTexture = kinect.colorTexture;
        }
    }
}

