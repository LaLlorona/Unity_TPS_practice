using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DampTest : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 100F;
    private Vector3 velocity = Vector3.zero;
    Vector3 targetPosition;

    private void Start()
    {
        targetPosition = transform.position + new Vector3(0, 10, 0);
    }



    void Update()
    {
        // Define a target position above and behind the target transform


        // Smoothly move the camera towards that target position
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

    }
}
