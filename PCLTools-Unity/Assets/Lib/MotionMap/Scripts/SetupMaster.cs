using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetupMaster : MonoBehaviour {

    public GameObject mappity;
    public GameObject motionMap;

    void Start()
    {
        Invoke("enableMappity", 1);
        Invoke("enableMotionMap", 2);
    }

    void enableMappity()
    {
        mappity.SetActive(true);
    }

    void enableMotionMap()
    {
        motionMap.SetActive(true);
    }
}
