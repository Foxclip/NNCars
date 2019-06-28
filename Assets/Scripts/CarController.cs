using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    public List<AxleInfo> axleInfos; // the information about each individual axle
    public float maxMotorTorque = 1000.0f; // maximum torque the motor can apply to wheel
    public float maxSteeringAngle = 45.0f; // maximum steer angle the wheel can have
    public GameObject carSpawnPoint;
    public Transform[] rayOrigins;
    public GameObject gameControllerObject;
    public int inputSmooting = 1;
    public double inputDelay = 0.1;
    public double outputDelay = 0.1;
    public bool averagedInput = true;

    [HideInInspector]
    public NeuralNetwork neuralNetwork;

    private const double FPS = 60.0;
    private double inputSteps;
    private double outputSteps;

    private GameController gameController;
    private Rigidbody rb;
    private Queue<List<double>> inputQueue;
    private Queue<List<double>> outputQueue;

    public void Start()
    {
        Physics.queriesHitBackfaces = true;
        gameController = gameControllerObject.GetComponent<GameController>();
        rb = GetComponent<Rigidbody>();
        inputSteps = (int)(inputDelay * FPS);
        outputSteps = (int)(outputDelay * FPS);

        ResetQueues();

    }

    public void ResetQueues()
    {

        inputQueue = new Queue<List<double>>();
        for (int list_i = 0; list_i < inputSteps; list_i++)
        {
            List<double> emptyInput = new List<double>();
            for (int input_i = 0; input_i < GameController.INPUT_COUNT; input_i++)
            {
                emptyInput.Add(0.0);
            }
            inputQueue.Enqueue(emptyInput);

        }

        outputQueue = new Queue<List<double>>();
        for (int list_i = 0; list_i < outputSteps; list_i++)
        {
            List<double> emptyOutput = new List<double>();
            for (int output_i = 0; output_i < 2; output_i++)
            {
                emptyOutput.Add(0.0);
            }
            outputQueue.Enqueue(emptyOutput);

        }

    }

    public void FixedUpdate()
    {

        rb.isKinematic = false;

        List<double> NNInputs = new List<double>();

        foreach(Transform rayOrigin in rayOrigins)
        {
            RaycastHit hit;
            if(Physics.Raycast(rayOrigin.position, rayOrigin.forward, out hit))
            {
                NNInputs.Add(hit.distance);
                Debug.DrawRay(rayOrigin.position, rayOrigin.forward * hit.distance, Color.yellow);
            } else
            {
                NNInputs.Add(-1.0);
            }
        }

        //NNInputs.Add(gameController.nextCheckpoint);
        NNInputs.Add(rb.velocity.magnitude);

        //double carAngle = Mathf.Rad2Deg * (Mathf.Atan2(transform.forward.x, transform.forward.z));
        //double velocityAngle = Mathf.Rad2Deg * (Mathf.Atan2(rb.velocity.x, rb.velocity.z));
        //double angleOfAttack = (carAngle - velocityAngle) % 360;
        //Debug.Log("CarA: " + carAngle + "VelA: " + velocityAngle + " AoA: " + angleOfAttack);

        double totalSlip = 0.0;
        //foreach (AxleInfo axleInfo in axleInfos)
        //{
        //    WheelHit hit;
        //    axleInfo.leftWheel.GetGroundHit(out hit);
        //    totalSlip += hit.sidewaysSlip;
        //    axleInfo.rightWheel.GetGroundHit(out hit);
        //    totalSlip += hit.sidewaysSlip;
        //}
        AxleInfo rearAxle = axleInfos[1];
        WheelHit wheelHit;
        rearAxle.leftWheel.GetGroundHit(out wheelHit);
        totalSlip += wheelHit.sidewaysSlip;
        rearAxle.rightWheel.GetGroundHit(out wheelHit);
        totalSlip += wheelHit.sidewaysSlip;
        //Debug.Log(totalSlip);
        NNInputs.Add(totalSlip);
        //NNInputs.Add(gameController.passFitness);

        if(neuralNetwork.inputCount != NNInputs.Count)
        {
            throw new System.Exception("Input lists do not match: NN(" + neuralNetwork.inputCount + ") Inputs(" + NNInputs.Count + ")");
        }

        inputQueue.Enqueue(NNInputs);
        List<double> currentInputs = new List<double>();
        if(averagedInput)
        {
            List<double>[] inputs = inputQueue.ToArray();
            for(int input_i = 0; input_i < inputs[0].Count; input_i++)
            {
                double sum = 0.0;
                for(int list_i = 0; list_i < inputs.Length; list_i++)
                {
                    sum += inputs[list_i][input_i];
                }
                sum /= inputs.Length;
                currentInputs.Add(sum);
            }
            inputQueue.Dequeue();
        }
        else
        {
            currentInputs = inputQueue.Dequeue();
        }

        List <double> neuralNetworkOutput = neuralNetwork.Feedforward(currentInputs);
        outputQueue.Enqueue(neuralNetworkOutput);
        List<double> currentOutputs = outputQueue.Dequeue();

        float motor = maxMotorTorque * (float)currentOutputs[0];
        //if(motor < 0.0f)
        //{
        //    motor = 0.0f;
        //}
        float steering = maxSteeringAngle * (float)currentOutputs[1];
        //Debug.Log("Motor: " + motor + " Steering: " + steering);

        //motor = maxMotorTorque * Input.GetAxis("Vertical");
        //steering = maxSteeringAngle * Input.GetAxis("Horizontal");

        foreach (AxleInfo axleInfo in axleInfos)
        {

            for (int i = 0; i < inputSmooting; i++)
            {
                steering = (steering + axleInfo.leftWheel.steerAngle) / 2.0f;
            }

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

    void OnCollisionEnter(Collision collision)
    {
        gameController.collisionDetected = true;
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