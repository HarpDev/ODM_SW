using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportTrigger : MonoBehaviour
{
    public Transform waypoint;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {       
            other.transform.position = waypoint.position;
        }
    }
}