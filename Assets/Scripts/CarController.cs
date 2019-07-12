using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{

    //list of variables which can be set from StartupSettings
    public static List<Setting> settings = new List<Setting> {
        new BoolSetting("manualControl", false),
        new FloatSetting("maxMotorTorque", 1000.0f),
        new FloatSetting("maxSteeringAngle", 45.0f),
        new IntSetting("steeringSmoothing", 1),
        new FloatSetting("inputDelay", 0.1f),
        new FloatSetting("outputDelay", 0.1f),
        new BoolSetting("averagedInput", true)
    };

    //names of possible inputs and outputs of the neural network
    //list of toggles in StartupSettings will be generated based on these
    public static List<string> possibleInputs = new List<string> { "RayForward", "RayLeftFront45", "RayRightFront45", "RayLeft90", "RayRight90",
        "RayLeftFront22", "RayRightFront22", "RayLeft68", "RayRight68", "Speed", "FrontSlip", "RearSlip" };
    public static List<string> possibleOutputs = new List<string> { "motor", "steering" };

    public bool manualControl = false;                      //if by some reason manual keyboard control is needed
    public List<AxleInfo> axleInfos;                        //the information about each individual axle
    public float maxMotorTorque = 1000.0f;                  // maximum torque the motor can apply to wheel
    public float maxSteeringAngle = 45.0f;                  // maximum steer angle the wheel can have
    public GameObject carSpawnPoint;                        //place where car spawns every pass
    public Transform[] rayOrigins;                          //raycasts are made in these directions
    public GameObject gameControllerObject;                 //GameController object
    public int steeringSmoothing = 1;                       //how much steering is smoothed out, needed so steering will respond slower
    public double inputDelay = 0.1;                         //inputs are fed to neural network with this delay
    public double outputDelay = 0.1;                        //outputs are sent to the wheels, but they get there after this delay
    public bool averagedInput = true;                       //all values in the input queue are averaged, setting this to true will lead to smoother response of the neural network

    [HideInInspector]
    public NeuralNetwork neuralNetwork;                     //current neural network assigned to the car

    private const double FPS = 60.0;                        //frames per second, is used to calculate size of input/output queues
    private double inputSteps;                              //how long input queue is
    private double outputSteps;                             //how long ouput queue is

    private GameController gameController;                  //GameController script
    private Rigidbody rb;                                   //Rigidbody of the car
    private Queue<Dictionary<string, double>> inputQueue;   //all inputs are going through this queue
    private Queue<Dictionary<string, double>> outputQueue;  //all outputs are going through this queue

    public void Start()
    {

        //loading settings
        manualControl = StartupSettings.GetBoolSetting("manualControl");
        maxMotorTorque = StartupSettings.GetFloatSetting("maxMotorTorque");
        maxSteeringAngle = StartupSettings.GetFloatSetting("maxSteeringAngle");
        steeringSmoothing = StartupSettings.GetIntSetting("steeringSmoothing");
        inputDelay = StartupSettings.GetFloatSetting("inputDelay");
        outputDelay = StartupSettings.GetFloatSetting("outputDelay");
        averagedInput = StartupSettings.GetBoolSetting("averagedInput");

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

        inputQueue = new Queue<Dictionary<string, double>>();
        for (int list_i = 0; list_i < inputSteps; list_i++)
        {
            Dictionary<string, double> emptyInput = new Dictionary<string, double>();
            foreach (string inputName in StartupSettings.registeredInputs)
            {
                emptyInput.Add(inputName, 0.0);
            }
            inputQueue.Enqueue(emptyInput);

        }

        outputQueue = new Queue<Dictionary<string, double>>();
        for (int list_i = 0; list_i < outputSteps; list_i++)
        {
            Dictionary<string, double> emptyOutput = new Dictionary<string, double>();
            foreach (string outputName in StartupSettings.registeredOutputs)
            {
                emptyOutput.Add(outputName, 0.0);
            }
            outputQueue.Enqueue(emptyOutput);

        }

    }

    public void FixedUpdate()
    {

        //if rigidbody was set to kinematic, it has to be set to false
        rb.isKinematic = false;

        //inputs which will be fed to neural network
        Dictionary<string, double> NNInputs = new Dictionary<string, double>();

        //adding raycast lengths to list of inputs
        foreach(Transform rayOrigin in rayOrigins)
        {

            //name of neural network input, as opposed to name of the object in the scene
            string rayOriginName = rayOrigin.name.Replace(" ", "");

            if (!StartupSettings.registeredInputs.Contains(rayOriginName))
            {
                continue;
            }

            if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit))
            {
                NNInputs.Add(rayOriginName, hit.distance);
                Debug.DrawRay(rayOrigin.position, rayOrigin.forward * hit.distance, Color.yellow);
            }
            else
            {
                NNInputs.Add(rayOriginName, -1.0);
            }
        }

        //adding velocity
        if (StartupSettings.registeredInputs.Contains("Speed"))
        {
            NNInputs.Add("Speed", rb.velocity.magnitude);
        }

        //adding slip of the front wheels
        if (StartupSettings.registeredInputs.Contains("FrontSlip"))
        {
            double frontWheelSlip = 0.0;
            AxleInfo frontAxle = axleInfos[0];
            frontAxle.leftWheel.GetGroundHit(out WheelHit frontWheelHit);
            frontWheelSlip += frontWheelHit.sidewaysSlip;
            frontAxle.rightWheel.GetGroundHit(out frontWheelHit);
            frontWheelSlip += frontWheelHit.sidewaysSlip;
            NNInputs.Add("FrontSlip", frontWheelSlip);
        }

        //adding slip of the rear wheels
        if (StartupSettings.registeredInputs.Contains("RearSlip"))
        {
            double rearWheelsSlip = 0.0;
            AxleInfo rearAxle = axleInfos[1];
            rearAxle.leftWheel.GetGroundHit(out WheelHit rearWheelHit);
            rearWheelsSlip += rearWheelHit.sidewaysSlip;
            rearAxle.rightWheel.GetGroundHit(out rearWheelHit);
            rearWheelsSlip += rearWheelHit.sidewaysSlip;
            NNInputs.Add("RearSlip", rearWheelsSlip);
        }

        //inputs which come now will be put to the back of the input queue
        inputQueue.Enqueue(NNInputs);
        //and this is the dictionary for the inputs which will be fed to the neural network now
        Dictionary<string, double> currentInputs = new Dictionary<string, double>();
        if (averagedInput)
        {
            //converting queue to array
            Dictionary<string, double>[] inputs = inputQueue.ToArray();
            //dictionary for results
            foreach (string inputName in StartupSettings.registeredInputs)
            {
                currentInputs.Add(inputName, 0.0);
            }
            //summing input values
            foreach (Dictionary<string, double> input in inputs)
            {
                foreach (string inputName in StartupSettings.registeredInputs)
                {
                    currentInputs[inputName] += input[inputName];
                }
            }
            //dividing by length to find average
            foreach (string inputName in StartupSettings.registeredInputs)
            {
                currentInputs[inputName] /= inputs.Length;
            }
            inputQueue.Dequeue();
        }
        else
        {
            //if input is not averaged, we just take it from the back of the input queue
            currentInputs = inputQueue.Dequeue();
        }

        //feeding inputs to the neural network
        Dictionary<string, double> neuralNetworkOutput = neuralNetwork.Feedforward(currentInputs);

        //adding output of the neural network to the back of the output queue
        outputQueue.Enqueue(neuralNetworkOutput);

        //curent outputs are taken from the back of the output queue
        Dictionary<string, double> currentOutputs = outputQueue.Dequeue();

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
            if (StartupSettings.registeredOutputs.Contains("motor"))
            {
                motor = maxMotorTorque * (float)currentOutputs["motor"];
                //car should not go backwards
                motor = Mathf.Max(0.0f, motor);
            }
            if (StartupSettings.registeredOutputs.Contains("steering"))
            {
                steering = maxSteeringAngle * (float)currentOutputs["steering"];
            }
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
        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        visualWheel.transform.position = position;
        visualWheel.transform.rotation = rotation * Quaternion.Euler(0.0f, 0.0f, 90.0f);
    }



    //detects if car collided with a wall
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0060 // Remove unused parameter
    void OnCollisionEnter(Collision collision)
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore IDE0051 // Remove unused private members
    {
        gameController.CollisionDetected = true;
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