using UnityEngine;
using System.Collections;
using DG.Tweening;
using BK.PCL;


public class MotionMapCursor : MonoBehaviour
{

    public void setColor(Color c)
    {
        GetComponent<Renderer>().material.color = c;
    }
}
