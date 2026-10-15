using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtCam : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        if (!Camera.main.transform) return;
        transform.forward = Camera.main.transform.forward;
    }
}
