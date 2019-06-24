﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    public List<AxleInfo> axleInfos; // the information about each individual axle
    public float maxMotorTorque = 1000.0f; // maximum torque the motor can apply to wheel
    public float maxSteeringAngle = 45.0f; // maximum steer angle the wheel can have
    public GameObject carSpawnPoint;
    public Transform[] rayOrigins;

    [HideInInspector]
    public NeuralNetwork neuralNetwork;

    public void Start()
    {
        Physics.queriesHitBackfaces = true;
    }

    public void FixedUpdate()
    {

        List<double> hitDistances = new List<double>();

        foreach(Transform rayOrigin in rayOrigins)
        {
            RaycastHit hit;
            if(Physics.Raycast(rayOrigin.position, rayOrigin.forward, out hit))
            {
                hitDistances.Add(hit.distance);
                Debug.DrawRay(rayOrigin.position, rayOrigin.forward * hit.distance, Color.yellow);
            } else
            {
                hitDistances.Add(0.0);
            }
        }

        List <double> neuralNetworkOutput = neuralNetwork.Feedforward(hitDistances);
        float motor = maxMotorTorque * (float)neuralNetworkOutput[0];
        //if(motor < 0.0f)
        //{
        //    motor = 0.0f;
        //}
        float steering = maxSteeringAngle * (float)neuralNetworkOutput[1];
        //Debug.Log("Motor: " + motor + " Steering: " + steering);

        //motor = maxMotorTorque * Input.GetAxis("Vertical");
        //steering = maxSteeringAngle * Input.GetAxis("Horizontal");

        foreach (AxleInfo axleInfo in axleInfos)
        {
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }
            if (axleInfo.motor)
            {
                axleInfo.leftWheel.motorTorque = motor;
                axleInfo.rightWheel.motorTorque = motor;
            }
            ApplyLocalPositionToVisuals(axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheel);
        }
    }

    // finds the corresponding visual wheel
    // correctly applies the transform
    public void ApplyLocalPositionToVisuals(WheelCollider collider)
    {
        if (collider.transform.childCount == 0)
        {
            return;
        }

        Transform visualWheel = collider.transform.GetChild(0);

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        visualWheel.transform.position = position;
        visualWheel.transform.rotation = rotation * Quaternion.Euler(0.0f, 0.0f, 90.0f);
    }

}

[System.Serializable]
public class AxleInfo
{
    public WheelCollider leftWheel;
    public WheelCollider rightWheel;
    public bool motor; // is this wheel attached to motor?
    public bool steering; // does this wheel apply steer angle?
}