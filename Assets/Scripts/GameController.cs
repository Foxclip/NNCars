using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Class related to simulation.
/// </summary>
public class GameController : MonoBehaviour
{
#pragma warning disable IDE0044 // Add readonly modifier

    // UI text
    [SerializeField]
    private Text genRunPassText;
    [SerializeField]
    private Text passFitnessText;
    [SerializeField]
    private Text maxFitnessText;
    [SerializeField]
    private Text bestCarText;
    [SerializeField]
    private Text minTimeText;
    [SerializeField]
    private Text breakthroughCountText;
    [SerializeField]
    private Text timeText;
    [SerializeField]
    private Text speedDeathTimerText;
    [SerializeField]
    private Text fitnessDeathTimerText;

    [SerializeField]
    private GameObject carObject;                    // GameObject of the car
    [SerializeField]
    private string networksFolderPath = "Networks/"; // folder which neural networks are saved to

#pragma warning restore IDE0044 // Add readonly modifier

    private CarController carController;                                    // CarController script
    private Transform track;                                                // transform of the track
#pragma warning disable SA1214 // Readonly fields should appear before non-readonly fields
    private readonly List<Transform> checkpoints = new List<Transform>();   // list of all checkpoints in the track
#pragma warning restore SA1214 // Readonly fields should appear before non-readonly fields
    private Transform carSpawnPoint;                                        // where car will be placed before strting a pass
    private NeuralNetwork bestNetwork;                                      // best result of the simulation
    private double bestFitnessInThisPass = 0.0;                             // best fitness achieved in this pass, pass is ended if it does not imporve for some amount of time
    private List<Pass> passes;                                              // list of passes in the run, used to calculate fitness of the run
    private float fitnessDeathTimer = 0.0f;                                 // how much time passed since last improvement of bestFitnessInThisPass
    private float speedDeathTimer = 0.0f;                                   // how much time passed since speed was not too low
    private int generationIndex = 0;                                        // index of current generation
    private int runIndex = 0;                                               // index of current run
    private int passIndex = 0;                                              // index of current pass
    private double bestRunFitness = 0.0f;                                   // best fitness achieved in this simulation
    private int breakthroughGen = 0;                                        // index of generation where best fitness was achieved
    private int breakthroughRun = 0;                                        // index of run where best fitness was achieved
    private float distance = 0.0f;                                          // how much distance car has covered in this pass
    private float acceptedMinTime = -1.0f;                                  // how fast car was able to comlete the track, should be -1 if it hasn't completed it yet
    private Vector3 previousPosition;                                       // position of the car in previous frame
    private float steeringAmount = 0.0f;                                    // how much wheels have turned in this pass, needed to calculate steering penalty
    private float previousSteering = 0.0f;                                  // steering angle in previous frame
    private int breakthroughCount = 0;                                      // how much fitness improvements happened with this neural network
    private bool fastForward = false;                                       // whether fast forward function is activated

    /// <summary>
    /// Determines how run fitness is calculated.
    /// </summary>
    internal enum RunAcceptModes
    {
        /// <summary>
        /// Run fitness is fitness of the worst pass in the run.
        /// </summary>
        All,

        /// <summary>
        /// Run fitness is fitness of the median pass in the run.
        /// </summary>
        Median,
    }

    /// <summary>
    /// Current simulation settings.
    /// </summary>
    public static SimulationSettings Settings { get; set; } = new SimulationSettings();

    /// <summary>
    /// Current generation of neural netwroks.
    /// </summary>
    public List<NeuralNetwork> Generation { get; set; } = new List<NeuralNetwork>();

    /// <summary>
    /// Car collided with a wall.
    /// </summary>
    public bool CollisionDetected { get; set; } = false;

    /// <summary>
    /// Index of the next checkpoint.
    /// </summary>
    public int NextCheckpoint { get; set; } = 0;

    /// <summary>
    /// Fitness of the current pass.
    /// </summary>
    public double PassFitness { get; set; }

    /// <summary>
    /// How much time in seconds passed since beginning of the pass.
    /// </summary>
    public float Timer { get; set; } = 0.0f;

    private void Start()
    {
        // getting CarController
        this.carController = this.carObject.GetComponent<CarController>();

        // loading track
        Transform tracksParent = GameObject.Find("Tracks").transform;
        foreach (Transform t in tracksParent)
        {
            t.gameObject.SetActive(false);
        }
        this.track = tracksParent.GetChild(StartupSettings.TrackIndex);
        this.track.gameObject.SetActive(true);

        // loading spawn point
        this.carSpawnPoint = this.track.Find("Spawn");

        // loading checkpoints
        Transform checkpointsParent = this.track.Find("Checkpoints");
        foreach (Transform child in checkpointsParent)
        {
            this.checkpoints.Add(child);
        }

        // loading neural network or creating new one
        if (StartupSettings.SelectedNeuralNetwork != null)
        {
            this.bestNetwork = StartupSettings.SelectedNeuralNetwork;
            this.breakthroughCount = this.bestNetwork.BreakthroughCount;

            // if training is continued on the same track, there is no need to break all fitness records again
            // if it is different track, we have to start from zero
            if (this.bestNetwork.TrackName == this.track.name)
            {
                this.bestRunFitness = this.bestNetwork.Fitness;
            }
        }
        else
        {
            this.bestNetwork = new NeuralNetwork(StartupSettings.RegisteredInputs, StartupSettings.RegisteredOutputs, Settings.LayerCount, Settings.NeuronsInLayer);
        }
        this.bestNetwork.TrackName = this.track.name;

        // preparing simulation
        this.PreGeneration();
        this.PreRun();
        this.PrePass();
    }

    private void Update()
    {
        if (Input.GetKeyDown("space"))
        {
            // fast forward function
            if (this.fastForward)
            {
                Time.timeScale = 1.0f;
            }
            else
            {
                Time.timeScale = Settings.SpeedupTimeScale;
            }
            this.fastForward = !this.fastForward;
        }

        this.UpdateUIText();
    }

    private void FixedUpdate()
    {
        this.distance += Vector3.Distance(this.carObject.transform.position, this.previousPosition);
        this.previousPosition = this.carObject.transform.position;
        this.steeringAmount += Mathf.Abs(this.carController.SteeringAngle - this.previousSteering);
        this.previousSteering = this.carController.SteeringAngle;
        this.Timer += Time.fixedDeltaTime;

        // float addition is somewhat imprecise, so this is needed
        this.Timer = (float)Math.Round((decimal)this.Timer, 2, MidpointRounding.AwayFromZero);

        if (this.CheckDeathConditions())
        {
            return;
        }

        this.UpdateDeathTimers();

        this.AddBonuses();
    }

    private bool CheckDeathConditions()
    {
        if (this.fitnessDeathTimer > CarController.Settings.TerminationDelay)
        {
            this.NextPass();
            return true;
        }
        if (this.speedDeathTimer > CarController.Settings.TerminationDelay)
        {
            this.NextPass();
            return true;
        }
        if (this.carObject.transform.position.y < 0.0f)
        {
            this.NextPass();
            return true;
        }
        if (CarController.Settings.DieOnCollision && this.CollisionDetected)
        {
            this.NextPass();
            return true;
        }
        if (this.NextCheckpoint >= this.checkpoints.Count)
        {
            this.NextPass();
            return true;
        }
        return false;
    }

    private void UpdateDeathTimers()
    {
        // fitness timer
        if (this.PassFitness <= this.bestFitnessInThisPass)
        {
            this.fitnessDeathTimer += Time.fixedDeltaTime;
        }
        else
        {
            this.fitnessDeathTimer = 0.0f;
            this.bestFitnessInThisPass = this.PassFitness;
        }

        // speed timer
        double currentCarVelocity = this.carObject.GetComponent<Rigidbody>().velocity.magnitude;
        if (currentCarVelocity < CarController.Settings.TerminationSpeed)
        {
            this.speedDeathTimer += Time.fixedDeltaTime;
        }
        else
        {
            this.speedDeathTimer = 0.0f;
        }
    }

    private void AddBonuses()
    {
        // has to update next checkpoint first
        if (this.NextCheckpoint < this.checkpoints.Count)
        {
            if (Vector3.Distance(this.carObject.transform.position, this.checkpoints[this.NextCheckpoint].position) < CarController.Settings.CheckpointReachDistance)
            {
                this.NextCheckpoint++;
            }
        }

        // distance bonus
        double distanceBonus = 0.0;
        if (this.NextCheckpoint < this.checkpoints.Count)
        {
            float distanceToNextCheckpoint = Vector3.Distance(this.carObject.transform.position, this.checkpoints[this.NextCheckpoint].position);
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * CarController.Settings.DistanceBonusWeight;
        }

        // checkpoint bonus
        double checkpointBonus = this.NextCheckpoint * CarController.Settings.CheckpointBonusWeight;

        this.PassFitness = checkpointBonus + distanceBonus;
    }

    private void UpdateUIText()
    {
        this.genRunPassText.text = "GEN " + this.generationIndex + " RUN " + this.runIndex + " PASS " + this.passIndex;
        this.passFitnessText.text = "PASS FITNESS: " + this.PassFitness;
        this.maxFitnessText.text = "MAX FITNESS: " + this.bestRunFitness;
        this.bestCarText.text = "BEST: GEN " + this.breakthroughGen + " RUN " + this.breakthroughRun;
        this.minTimeText.text = "MIN TIME: " + this.acceptedMinTime;
        this.breakthroughCountText.text = "BREAKTHROUGHS: " + this.breakthroughCount;
        this.timeText.text = $"TIME: {this.Timer:0.00}";
        this.speedDeathTimerText.text = string.Format("SPD: {0:0.0}", this.speedDeathTimer);
        this.fitnessDeathTimerText.text = string.Format("FIT: {0:0.0}", this.fitnessDeathTimer);
    }

    private void CheckBestResult(double runFitness, float runMinTime)
    {
        // has to be in here so it will be saved in the file
        this.Generation[this.runIndex].Fitness = runFitness;
        this.Generation[this.runIndex].MinTime = runMinTime;

        // updating fitness and best results
        // if it is same neural network (run 0), result is not accepted, except if it is first update
        if (runFitness > this.bestRunFitness && (this.runIndex > 0 || this.bestRunFitness == 0.0))
        {
            // new breakthrough, new breakthough count
            this.breakthroughCount++;
            this.Generation[this.runIndex].BreakthroughCount = this.breakthroughCount;

            // updating index of best run
            this.bestRunFitness = runFitness;
            this.breakthroughGen = this.generationIndex;
            this.breakthroughRun = this.runIndex;

            // saving best neural network to file
            string trackName = this.track.name.Replace(" ", string.Empty);
            string dateString = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string minTimeString = $"t{runMinTime:0.00}";
            string bcString = "bc" + this.breakthroughCount;
            string genRunString = "g" + this.generationIndex + "r" + this.runIndex;
            string filePath = trackName + "_" + dateString + "_" + minTimeString + "_" + bcString + "_" + genRunString + ".txt";
            Directory.CreateDirectory(this.networksFolderPath);
            this.Generation[this.runIndex].SaveToFile(StartupSettings.NetworksFolderPath + "/" + filePath);
        }

        // updating best time
        // if run 0 improves time, it will be updated, but max fitness will not
        if (runMinTime >= 0.0 && (runMinTime < this.acceptedMinTime || this.acceptedMinTime < 0.0))
        {
            this.acceptedMinTime = runMinTime;
        }
    }

    // is called after generation is complete
    private void PostGeneration()
    {
        this.Generation.Sort((x, y) => -x.Fitness.CompareTo(y.Fitness));

        // if we have new best result
        if (this.Generation[0].Fitness > this.bestNetwork.Fitness)
        {
            this.bestNetwork = this.Generation[0];
        }
    }

    // called just after starting new generation
    private void PreGeneration()
    {
        // creating new neural networks
        List<NeuralNetwork> newGeneration = new List<NeuralNetwork>();
        for (int i = 0; i < Settings.PopulationSize; i++)
        {
            NeuralNetwork newNetwork;

            // WARNING: results of the run 0 are not counted, so if you will make first network in generation mutate, make results of the run 0 count
            if (i == 0)
            {
                newNetwork = this.bestNetwork.Copy();
            }
            else
            {
                newNetwork = this.bestNetwork.Copy();
            }
            newNetwork.Mutate(1, Settings.MaxMutation * Math.Pow((double)i / Settings.PopulationSize, Settings.MutationPower));
            newGeneration.Add(newNetwork);
        }

        // swapping generations
        this.Generation = new List<NeuralNetwork>(newGeneration);

        this.runIndex = 0;
    }

    // proceed to the next generation
    private void NextGeneration()
    {
        this.PostGeneration();

        this.generationIndex++;

        this.PreGeneration();
    }

    // called after run is complete
    private void PostRun()
    {
        /* calculating fitness of the run*/

        float runMinTime = -1.0f;
        double runFitness = 0.0;

        // if mode is Median, we take median pass
        if (Settings.RunAcceptMode == RunAcceptModes.Median)
        {
            Pass medianPass = this.passes[(this.passes.Count - 1) / 2];
            runFitness = medianPass.Fitness;
            if (medianPass.NextCheckpoint >= this.checkpoints.Count)
            {
                runMinTime = medianPass.Time;
            }
        }

        // if it is All, we take the worst pass
        else if (Settings.RunAcceptMode == RunAcceptModes.All)
        {
            this.passes.Sort((x, y) => x.Fitness.CompareTo(y.Fitness));
            runFitness = this.passes[0].Fitness;
            if (this.passes[0].NextCheckpoint >= this.checkpoints.Count)
            {
                runMinTime = this.passes[0].Time;
            }
        }

        // check if result of this run is the best one
        this.CheckBestResult(runFitness, runMinTime);
    }

    // called just after starting new run
    private void PreRun()
    {
        // initializing neural network
        NeuralNetwork network = this.Generation[this.runIndex];
        this.carController.NeuralNetwork = network;

        // list of passes has to be cleared
        this.passes = new List<Pass>();

        this.passIndex = 0;
    }

    // called after completing a pass
    private bool PostPass()
    {
        // speed and time bonuses
        // if the car completes the track, it gets time bonus
        // otherwise, it gets speed bonus
        double speedBonus;
        double timeBonus;
        if (this.NextCheckpoint < this.checkpoints.Count)
        {
            double averageSpeed = this.distance / this.Timer;
            speedBonus = Math.Tanh(averageSpeed * CarController.Settings.SpeedBonusWeight);
            timeBonus = 0.0;
        }
        else
        {
            speedBonus = 0.0;
            timeBonus = 1.0 / (this.Timer + 1.0);
        }

        this.PassFitness += speedBonus;
        this.PassFitness += timeBonus;

        // steering bonus
        double steeringBonus = 1.0 / (this.steeringAmount + 1) * CarController.Settings.SteeringPenaltyWeight;
        this.PassFitness += steeringBonus;

        Debug.Log($"Speed bonus: {speedBonus} Time bonus: {timeBonus} Steering bonus: {steeringBonus}");

        // adding this pass to the list
        Pass pass = new Pass
        {
            Fitness = this.PassFitness,
            Time = this.Timer,
            NextCheckpoint = this.NextCheckpoint,
        };
        this.passes.Add(pass);

        // if car was not able to improve best result, and we take the worst pass in the run as fitness of the whole run, there is no point in continuing this run
        if (Settings.RunAcceptMode == RunAcceptModes.All && this.PassFitness <= this.bestRunFitness)
        {
            return true;
        }

        return false;
    }

    private void PrePass()
    {
        // resetting car parameters
        this.carObject.transform.position = this.carSpawnPoint.transform.position;
        this.carObject.transform.rotation = this.carSpawnPoint.transform.rotation;
        this.carObject.GetComponent<Rigidbody>().velocity = new Vector3(0.0f, 0.0f, 0.0f);
        this.carObject.GetComponent<Rigidbody>().angularVelocity = new Vector3(0.0f, 0.0f, 0.0f);
        this.fitnessDeathTimer = 0.0f;
        this.speedDeathTimer = 0.0f;
        this.bestFitnessInThisPass = 0.0;
        this.distance = 0.0f;
        this.previousPosition = this.carSpawnPoint.transform.position;
        this.Timer = 0.0f;
        this.NextCheckpoint = 0;
        this.CollisionDetected = false;
        this.PassFitness = 0;
        this.carController.ResetQueues();

        // randomized rotation
        this.carObject.transform.rotation *= Quaternion.Euler(Vector3.up * (float)Utils.MapRange(this.passIndex, 0, Settings.PassCount - 1, Settings.RandomAngleMin, Settings.RandomAngleMax));

        Debug.Log($"Gen {this.generationIndex} Run {this.runIndex} Pass {this.passIndex}");
    }

    // proceed to the next run
    private void NextRun()
    {
        this.PostRun();

        this.runIndex++;

        if (this.runIndex > Settings.PopulationSize - 1)
        {
            this.NextGeneration();
        }

        this.PreRun();
    }

    // proceed to the next pass
    // notice that run can be aborted early
    private void NextPass()
    {
        bool runAborted = this.PostPass();

        this.passIndex++;

        if (this.passIndex > Settings.PassCount - 1 || runAborted)
        {
            this.NextRun();
        }

        this.PrePass();
    }

    // this is needed to calculate fitness of the run
    private struct Pass
    {
        public double Fitness;
        public float Time;
        public double NextCheckpoint;
    }

    /// <summary>
    /// Settings which are saved and loaded from config file.
    /// </summary>
    [DataContract(Name = "SimulationSettings")]
    public class SimulationSettings : StartupSettings.SettingList
    {
        /// <summary>
        /// Number of hidden layers neural network will have.
        /// </summary>
        [DataMember]
        public int LayerCount { get; set; } = 1;

        /// <summary>
        /// Number of neurons in hidden layers.
        /// </summary>
        [DataMember]
        public int NeuronsInLayer { get; set; } = 16;

        /// <summary>
        /// Number of diffrerent neural networks in generation.
        /// </summary>
        [DataMember]
        public int PopulationSize { get; set; } = 10;

        /// <summary>
        /// To improve stability of the solution, several passes are made for each neural network.
        /// </summary>
        [DataMember]
        public int PassCount { get; set; } = 3;

        /// <summary>
        /// How likely small mutations are.
        /// </summary>
        [DataMember]
        public float MutationPower { get; set; } = 10.0f;

        /// <summary>
        /// Maximal amount of mutation in generation.
        /// </summary>
        [DataMember]
        public float MaxMutation { get; set; } = 1.0f;

        /// <summary>
        /// When pressing Space, simulation will speed up.
        /// </summary>
        [DataMember]
        public float SpeedupTimeScale { get; set; } = 100.0f;

        /// <summary>
        /// Minimal angle of random rotation in the beginning of each pass.
        /// </summary>
        [DataMember]
        public float RandomAngleMin { get; set; } = -25.0f;

        /// <summary>
        /// Maximal angle of random rotation in the beginning of each pass.
        /// </summary>
        [DataMember]
        public float RandomAngleMax { get; set; } = 25.0f;

        /// <summary>
        /// If by some reason manual keyboard control is needed.
        /// </summary>
        [DataMember]
        public bool ManualControl { get; set; } = false;

        /// <summary>
        /// Determines how fitness of the run is calculated.
        /// </summary>
        [DataMember]
        internal RunAcceptModes RunAcceptMode { get; set; }

    }
}
