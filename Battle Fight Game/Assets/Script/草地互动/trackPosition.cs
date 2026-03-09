using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class trackPosition : MonoBehaviour
{
    private GameObject tracker;

    private Material grassMat;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        grassMat = GetComponent<Renderer>().material;
        tracker = GameObject.Find("Tracker");
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 trackerPos = tracker.GetComponent<Transform>().position;

        grassMat.SetVector("_TrackerPos", trackerPos);
    }
}
