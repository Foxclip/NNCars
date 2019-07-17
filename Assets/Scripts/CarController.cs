using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;

/// <summary>
/// For controlling the car.
/// </summary>
public class CarController : MonoBehaviour
{
    private const double FPS = 60.0;                        // frames per second, is used to calculate size of input/output queues

#pragma warning disable IDE0044 // Add readonly modifier
    [SerializeField]
    private Transform[] rayOrigins;                         // raycasts are made in these directions
    [SerializeField]
    private List<AxleInfo> axleInfos;                       // the information about each individual axle
    [SerializeField]
    private GameObject gameControllerObject;                // GameController object
#pragma warning restore IDE0044 // Add readonly modifier

    private int inputSteps;                              // how long input queue is
    private int outputSteps;                             // how long ouput queue is

    private GameController gameController;                  // GameController script
    private Rigidbody rb;                                   // Rigidbody of the car
    private Dictionary<string, Queue<double>> inputQueues;   // all inputs are going through this queue
    private Dictionary<string, Queue<double>> outputQueues;  // all outputs are going through this queue

    /// <summary>
    /// Car settings.
    /// </summary>
    public static CarSettings Settings { get; set; } = new CarSettings();

    /// <summary>
    /// Names of possible inputs of the neural network.
    /// List of toggles in StartupSettings will be generated based on these.
    /// </summary>
    public static List<string> PossibleInputs { get; set; } = new List<string>
    {
        "RayForward",
        "RayLeftFront45", "RayRightFront45",
        "RayLeft90", "RayRight90",
        "RayLeftFront22", "RayRightFront22",
        "RayLeft68", "RayRight68",
        "Speed", "FrontSlip", "RearSlip", "Steering",
    };

    /// <summary>
    /// Names of possible outputs of the neural network.
    /// List of toggles in StartupSettings will be generated based on these.
    /// </summary>
    public static List<string> PossibleOutputs { get; set; } = new List<string> { "motor", "steering" };

    /// <summary>
    /// Current neural network assigned to the car.
    /// </summary>
    public NeuralNetwork NeuralNetwork { get; set; }

    /// <summary>
    /// Place where car spawns every pass.
    /// </summary>
    public GameObject CarSpawnPoint { get; set; }

    /// <summary>
    /// Fills input and output queues with zeroes.
    /// Should be called before starting a new pass.
    /// </summary>
    public void ResetQueues()
    {
        // input queues
        this.inputQueues = new Dictionary<string, Queue<double>>();
        foreach (string inputName in PossibleInputs)
        {
            string firstDerivativeName = inputName + "_D^1";
            string secondDerivativeName = inputName + "_D^2";
            this.inputQueues.Add(inputName, new Queue<double>());
            this.inputQueues.Add(firstDerivativeName, new Queue<double>());
            this.inputQueues.Add(secondDerivativeName, new Queue<double>());
            for (int list_i = 0; list_i < this.inputSteps; list_i++)
            {
                this.inputQueues[inputName].Enqueue(0.0);
                this.inputQueues[firstDerivativeName].Enqueue(0.0);
                this.inputQueues[secondDerivativeName].Enqueue(0.0);
            }
        }

        // output queues
        this.outputQueues = new Dictionary<string, Queue<double>>();
        foreach (string outputName in PossibleOutputs)
        {
            this.outputQueues.Add(outputName, new Queue<double>());
            for (int list_i = 0; list_i < this.outputSteps; list_i++)
            {
                this.outputQueues[outputName].Enqueue(0.0);
            }
        }
    }

    /// <summary>
    /// Finds the corresponding visual wheel and correctly applies the transform.
    /// </summary>
    /// <param name="collider">WheelCollide which sets position of the visual wheel.</param>
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

    private void Start()
    {
        // Raycasts will hit backfaces of objects
        Physics.queriesHitBackfaces = true;

        // getting components
        this.gameController = this.gameControllerObject.GetComponent<GameController>();
        this.rb = this.GetComponent<Rigidbody>();

        // calculating lengths of input/output queues
        this.inputSteps = (int)(Settings.InputDelay * FPS);
        this.outputSteps = (int)(Settings.OutputDelay * FPS);

        // input/output queues should be reset before starting, since neural network takes values from the front of the queue
        this.ResetQueues();
    }

    private void FixedUpdate()
    {
        // if rigidbody was set to kinematic, it has to be set to false
        this.rb.isKinematic = false;

        // adding raycast lengths to list of inputs
        foreach (Transform rayOrigin in this.rayOrigins)
        {
            // name of neural network input, as opposed to name of the object in the scene
            string rayOriginName = rayOrigin.name.Replace(" ", string.Empty);

            // adding ray length
            if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit))
            {
                this.inputQueues[rayOriginName].Enqueue(hit.distance);
                Debug.DrawRay(rayOrigin.position, rayOrigin.forward * hit.distance, Color.yellow);
            }
            else
            {
                this.inputQueues[rayOriginName].Enqueue(-1.0);
            }
        }

        // adding velocity
        this.inputQueues["Speed"].Enqueue(this.rb.velocity.magnitude);

        // adding slip of the front wheels
        double frontWheelSlip = 0.0;
        AxleInfo frontAxle = this.axleInfos[0];
        frontAxle.LeftWheel.GetGroundHit(out WheelHit frontWheelHit);
        frontWheelSlip += frontWheelHit.sidewaysSlip;
        frontAxle.RightWheel.GetGroundHit(out frontWheelHit);
        frontWheelSlip += frontWheelHit.sidewaysSlip;
        this.inputQueues["FrontSlip"].Enqueue(frontWheelSlip);

        // adding slip of the rear wheels
        double rearWheelSlip = 0.0;
        AxleInfo rearAxle = this.axleInfos[1];
        rearAxle.LeftWheel.GetGroundHit(out WheelHit rearWheelHit);
        rearWheelSlip += rearWheelHit.sidewaysSlip;
        rearAxle.RightWheel.GetGroundHit(out rearWheelHit);
        rearWheelSlip += rearWheelHit.sidewaysSlip;
        this.inputQueues["RearSlip"].Enqueue(rearWheelSlip);

        // adding steering
        this.inputQueues["Steering"].Enqueue(this.axleInfos[0].LeftWheel.steerAngle / Settings.MaxSteeringAngle);

        // adding first derivatives
        List<string> firstDerivativeNames = new List<string>();
        foreach (string inputName in PossibleInputs)
        {
            double[] inputQueueArray = this.inputQueues[inputName].ToArray();
            double value1 = inputQueueArray[inputQueueArray.Length - 1];
            double value2 = inputQueueArray[inputQueueArray.Length - 2];
            double firstDerivative = value2 - value1;
            string firstDerivativeName = inputName + "_D^1";
            this.inputQueues[firstDerivativeName].Enqueue(firstDerivative);
            firstDerivativeNames.Add(firstDerivativeName);
        }

        // adding second derivatives
        foreach (string firstDerivativeName in firstDerivativeNames)
        {
            double[] inputQueueArray = this.inputQueues[firstDerivativeName].ToArray();
            double value1 = inputQueueArray[inputQueueArray.Length - 1];
            double value2 = inputQueueArray[inputQueueArray.Length - 2];
            double secondDerivative = value2 - value1;
            string secondDerivativeName = firstDerivativeName.Split('_')[0] + "_D^2";
            this.inputQueues[secondDerivativeName].Enqueue(secondDerivative);
        }

        // averaging inputs
        Dictionary<string, double> currentInputs = new Dictionary<string, double>();
        if (Settings.AveragedInput)
        {
            foreach (string inputName in this.inputQueues.Keys)
            {
                if (Settings.AveragedInput)
                {
                    currentInputs.Add(inputName, this.inputQueues[inputName].Average());
                    this.inputQueues[inputName].Dequeue();
                }
                else
                {
                    currentInputs.Add(inputName, this.inputQueues[inputName].Dequeue());
                }
            }
        }

        // filtering inputs
        Dictionary<string, double> filteredInputs = new Dictionary<string, double>();
        foreach (string inputName in currentInputs.Keys)
        {
            if (StartupSettings.RegisteredInputs.Contains(inputName))
            {
                filteredInputs.Add(inputName, currentInputs[inputName]);
            }
        }

        // feeding inputs to the neural network
        Dictionary<string, double> neuralNetworkOutput = this.NeuralNetwork.Feedforward(filteredInputs);

        // adding outputs of the neural network to the back of the output queues
        foreach (string outputName in neuralNetworkOutput.Keys)
        {
            this.outputQueues[outputName].Enqueue(neuralNetworkOutput[outputName]);
        }

        // curent outputs are taken from the back of the output queue
        Dictionary<string, double> currentOutputs = new Dictionary<string, double>();
        foreach (string outputName in neuralNetworkOutput.Keys)
        {
            currentOutputs.Add(outputName, this.outputQueues[outputName].Dequeue());
        }

        // choosing between manual and automatic control
        float motor = 0.0f;
        float steering = 0.0f;
        if (Settings.ManualControl)
        {
            motor = Settings.MaxMotorTorque * Input.GetAxis("Vertical");
            steering = Settings.MaxSteeringAngle * Input.GetAxis("Horizontal");
        }
        else
        {
            if (StartupSettings.RegisteredOutputs.Contains("motor"))
            {
                motor = Settings.MaxMotorTorque * (float)currentOutputs["motor"];

                // car should not go backwards
                motor = Mathf.Max(0.0f, motor);
            }
            if (StartupSettings.RegisteredOutputs.Contains("steering"))
            {
                steering = Settings.MaxSteeringAngle * (float)currentOutputs["steering"];
            }
        }

        // setting values to the wheels
        foreach (AxleInfo axleInfo in this.axleInfos)
        {
            // smoothing steering by repeatedly averaging target position with position of the wheels
            for (int i = 0; i < Settings.SteeringSmoothing; i++)
            {
                steering = (steering + axleInfo.LeftWheel.steerAngle) / 2.0f;
            }

            // setting values of the wheel colliders
            if (axleInfo.Steering)
            {
                axleInfo.LeftWheel.steerAngle = steering;
                axleInfo.RightWheel.steerAngle = steering;
            }
            if (axleInfo.Motor)
            {
                axleInfo.LeftWheel.motorTorque = motor;
                axleInfo.RightWheel.motorTorque = motor;
            }

            // visual wheels have to be updated
            this.ApplyLocalPositionToVisuals(axleInfo.LeftWheel);
            this.ApplyLocalPositionToVisuals(axleInfo.RightWheel);
        }
    }

    // detects if car collided with a wall
    private void OnCollisionEnter(Collision collision)
    {
        this.gameController.CollisionDetected = true;
    }

    /// <summary>
    /// Class representing one wheel axle of the car.
    /// </summary>
    [System.Serializable]
    public class AxleInfo
    {
#pragma warning disable SA1401 // Fields should be private
        /// <summary>
        /// Left wheel of an axle.
        /// </summary>
        public WheelCollider LeftWheel;

        /// <summary>
        /// Right wheel of an axle.
        /// </summary>
        public WheelCollider RightWheel;

        /// <summary>
        /// Whether axle is motorized.
        /// </summary>
        public bool Motor;

        /// <summary>
        /// Whether axle is steerable.
        /// </summary>
        public bool Steering;
#pragma warning restore SA1401 // Fields should be private
    }

    /// <summary>
    /// Car settings which will be loaded/saved to config file.
    /// </summary>
    [DataContract(Name = "CarSettings")]
    public class CarSettings : StartupSettings.SettingList
    {
        /// <summary>
        /// If by some reason manual keyboard control is needed.
        /// </summary>
        [DataMember]
        public bool ManualControl { get; set; } = false;

        /// <summary>
        /// Maximum torque the motor can apply to wheel.
        /// </summary>
        [DataMember]
        public float MaxMotorTorque { get; set; } = 1000.0f;

        /// <summary>
        /// Maximum steer angle the wheel can have.
        /// </summary>
        [DataMember]
        public float MaxSteeringAngle { get; set; } = 45.0f;

        /// <summary>
        /// How much steering is smoothed out, needed so steering will respond slower.
        /// </summary>
        [DataMember]
        public int SteeringSmoothing { get; set; } = 1;

        /// <summary>
        /// Inputs are fed to neural network with this delay.
        /// </summary>
        [DataMember]
        public float InputDelay { get; set; } = 0.1f;

        /// <summary>
        /// Outputs are sent to the wheels, but they get there after this delay.
        /// </summary>
        [DataMember]
        public float OutputDelay { get; set; } = 0.1f;

        /// <summary>
        /// All values in the input queue are averaged, setting this to true will lead to smoother response of the neural network.
        /// </summary>
        [DataMember]
        public bool AveragedInput { get; set; } = true;
    }
}