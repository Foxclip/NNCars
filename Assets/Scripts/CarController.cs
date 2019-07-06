using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    public bool manualControl = false;          //if by some reason manual control is needed
    public List<AxleInfo> axleInfos;            //the information about each individual axle
    public float maxMotorTorque = 1000.0f;      // maximum torque the motor can apply to wheel
    public float maxSteeringAngle = 45.0f;      // maximum steer angle the wheel can have
    public GameObject carSpawnPoint;            //place where car spawns every pass
    public Transform[] rayOrigins;              //raycasts are made in these directions
    public GameObject gameControllerObject;     //GameController object
    public int steeringSmoothing = 1;           //how much steering is smoothed out, needed so steering will respond slower
    public double inputDelay = 0.1;             //inputs are fed to neural network with this delay
    public double outputDelay = 0.1;            //outputs are sent to the wheels, but they get there after this delay
    public bool averagedInput = true;           //all values in the input queue are averaged, setting this to true will lead to smoother response of the neural network

    [HideInInspector]
    public NeuralNetwork neuralNetwork;         //current neural network assigned to the car

    private const double FPS = 60.0;            //frames per second, is used to calculate size of input/output queues
    private double inputSteps;                  //how long input queue is
    private double outputSteps;                 //how long ouput queue is

    private GameController gameController;      //GameController script
    private Rigidbody rb;                       //Rigidbody of the car
    private Queue<List<double>> inputQueue;     //all inputs are going through this queue
    private Queue<List<double>> outputQueue;    //all outputs are going through this queue

    public void Start()
    {
        //Raycasts will hit backfaces of objects
        Physics.queriesHitBackfaces = true;

        //getting some values
        gameController = gameControllerObject.GetComponent<GameController>();
        rb = GetComponent<Rigidbody>();

        //calculating lengths of input/output queues
        inputSteps = (int)(inputDelay * FPS);
        outputSteps = (int)(outputDelay * FPS);

        //input/output queues should be reset before starting, since neural network takes values from the front of the queue
        ResetQueues();

    }

    //fills input and output queues with zeroes
    //should be called before starting new pass
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

        //if rigidbody was set to kinematic, it has to be set to false
        rb.isKinematic = false;

        //inputs which will be fed to neural network
        List<double> NNInputs = new List<double>();

        //adding raycast lengths to list of inputs
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

        //adding velocity
        NNInputs.Add(rb.velocity.magnitude);

        //adding slip of the front wheels
        double frontWheelSlip = 0.0;
        AxleInfo frontAxle = axleInfos[0];
        WheelHit frontWheelHit;
        frontAxle.leftWheel.GetGroundHit(out frontWheelHit);
        frontWheelSlip += frontWheelHit.sidewaysSlip;
        frontAxle.rightWheel.GetGroundHit(out frontWheelHit);
        frontWheelSlip += frontWheelHit.sidewaysSlip;
        NNInputs.Add(frontWheelSlip);

        //adding slip of the rear wheels
        double rearWheelsSlip = 0.0;
        AxleInfo rearAxle = axleInfos[1];
        WheelHit rearWheelHit;
        rearAxle.leftWheel.GetGroundHit(out rearWheelHit);
        rearWheelsSlip += rearWheelHit.sidewaysSlip;
        rearAxle.rightWheel.GetGroundHit(out rearWheelHit);
        rearWheelsSlip += rearWheelHit.sidewaysSlip;
        NNInputs.Add(rearWheelsSlip);

        //if neural network has less or more inputs then in the input list
        if (neuralNetwork.inputCount != NNInputs.Count)
        {
            throw new System.Exception("Input lists do not match: NN(" + neuralNetwork.inputCount + ") Inputs(" + NNInputs.Count + ")");
        }

        //inputs which come now will be put to the back of the input queue
        inputQueue.Enqueue(NNInputs);
        //and this is the list for the inputs which will be fed to the neural network now
        List<double> currentInputs = new List<double>();
        if(averagedInput)
        {
            //averaging all inputs available in the input queue
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
            //if input is not averaged, we just take it from the back of the input queue
            currentInputs = inputQueue.Dequeue();
        }

        //feeding inputs to the neural network
        List <double> neuralNetworkOutput = neuralNetwork.Feedforward(currentInputs);

        //adding output of the neural network to the back of the output queue
        outputQueue.Enqueue(neuralNetworkOutput);

        //curent outputs are taken from the back of the output queue
        List<double> currentOutputs = outputQueue.Dequeue();

        //choosing between manual and automatic control
        float motor = 0.0f;
        float steering = 0.0f;
        if (manualControl)
        {
            motor = maxMotorTorque * Input.GetAxis("Vertical");
            steering = maxSteeringAngle * Input.GetAxis("Horizontal");
        }
        else
        {
            motor = maxMotorTorque * (float)currentOutputs[0];
            steering = maxSteeringAngle * (float)currentOutputs[1];
        }

        //setting values to the wheels
        foreach (AxleInfo axleInfo in axleInfos)
        {

            //smoothing steering by repeatedly averaging target position with position of the wheels
            for (int i = 0; i < steeringSmoothing; i++)
            {
                steering = (steering + axleInfo.leftWheel.steerAngle) / 2.0f;
            }

            //setting values of the wheel colliders
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

            //visual wheels have to be updated
            ApplyLocalPositionToVisuals(axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheel);
        }
    }

    //finds the corresponding visual wheel
    //correctly applies the transform
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

    //detects if car collided with a wall
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
    public bool motor;                  //is this wheel attached to motor?
    public bool steering;               //does this wheel apply steer angle?
}