using UnityEngine;
using System.Collections.Generic;
using UnityOSC;

[RequireComponent(typeof(Collider))]
public class MotionMapZone : MonoBehaviour {

    public string id;
    public GameObject[] objects;
    public List<MMSelectableObject> mmos;

    [HideInInspector]
    public float overStartTime;
    [HideInInspector]
    public bool isOverInThisFrame; //to verify for not over event (without setOver on each frame)

    public bool over;
    public bool selected;
   
    [Header("Selection")]
    public float selectionProgression;
    public float autoDeselectOnTime = 5;

	// Use this for initialization
	void Start () {
        if (id == "") id = gameObject.name;

        mmos = new List<MMSelectableObject>();
        foreach (GameObject go in objects)
        {
            mmos.AddRange(go.GetComponents<MMSelectableObject>());
        }
	}
	

    public void setOver(bool value)
    {
        if (over == value) return;

        over = value;
        foreach (MMSelectableObject mmo in mmos) if(mmo != null) mmo.overChanged(value);
        if (!selected && value)  overStartTime = Time.time;
        
        OSCMessage m = new OSCMessage("/zone/"+id+"/over");
        m.Append(value?1:0);
        OSCMaster.sendMessage(m);

    }

    public void setSelectionProgression(float value)
    {
        if (!selected)
        { 
            value = Mathf.Clamp01(value);
            if (value == selectionProgression)
            {
                // Debug.Log("Same value !");
                return;
            }

            foreach (MMSelectableObject mmo in mmos)  if(mmo != null) mmo.selectionProgress(value);

            selectionProgression = value;
            OSCMessage m = new OSCMessage("/zone/" + id + "/selectionProgress");
            m.Append(selectionProgression);
            OSCMaster.sendMessage(m);
        }
    }

    public void setSelected(bool value)
    {
        if (selected == value) return;

        selected = value;
        foreach (MMSelectableObject mmo in mmos) if(mmo != null) mmo.selectionChanged(value);

        OSCMessage m = new OSCMessage(value?"/zone/"+id+"/selected":"/zone/"+id+"/deselected");
        OSCMaster.sendMessage(m);

        if(selected) Invoke("deselect", Mathf.Max(autoDeselectOnTime, 1));
    }

    public void deselect()
    {
        setSelected(false);
    }

    void OnDrawGizmos()
    {
        Color c= selected?Color.green:(isOverInThisFrame?Color.yellow:Color.cyan);
        c.a = .1f;
        Gizmos.color = c;
        Gizmos.DrawCube(transform.position, transform.lossyScale);
        Gizmos.color = new Color(c.r, c.g, c.b,.3f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }

    void OnDrawGizmosSelected()
    {
        Color c = Color.cyan;
        c.a = .3f;
        Gizmos.color = c;
        Gizmos.DrawCube(transform.position, transform.lossyScale);
        Gizmos.color = new Color(c.r, c.g, c.b, .3f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
  }
