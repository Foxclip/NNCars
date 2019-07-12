// <copyright file="GameController.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    private int layerCount = 1;                      // number of hidden layers neural network will have
    private int neuronsInLayer = 16;                 // number of neurons in hidden layers
    private int populationSize = 10;                 // number of diffrerent neural networks in generation
    private int passCount = 3;                       // to improve stability of the solution, several passes are made for each neural network
    private float mutationPower = 10;                // how likely small mutations are
    private float maxMutation = 1;                   // maximal amount of mutation in generation
    private float speedupTimeScale = 10;             // when pressing Space, simulation will speed up
    private float checkpointReachDistance = 3.0f;    // checkpoint is counted as reached when car is within this distance
    private double randomAngleMin = -25.0;           // minimal angle of random rotation in the beginning of each pass
    private double randomAngleMax = 25.0;            // maximal angle

    private RunAcceptMode runAcceptMode;             // determines how fitness of the run is calculated

    private double terminationDelay = 1.0;           // pass is ended if car's speed is below termination speed or fitness does not improve for this amount of time
    private double terminationSpeed = 0.2;           // what speed is too low
    private double checkpointBonusWeight = 100.0;    // weight of the checkpoint bonus
    private double distanceBonusWeight = 10.0;       // weight of the distance bonus
    private double speedBonusWeight = 0.01;          // weight of the speed bonus

    private CarController carController;                                    // CarController script
    private Transform track;                                                // transform of the track
#pragma warning disable SA1214 // Readonly fields should appear before non-readonly fields
    private readonly List<Transform> checkpoints = new List<Transform>();   // list of all checkpoints in the track
#pragma warning restore SA1214 // Readonly fields should appear before non-readonly fields
    private Transform carSpawnPoint;                                        // where car will be placed before strting a pass
    private NeuralNetwork bestNetwork;                                      // best result of the simulation
    private double bestFitnessInThisPass = 0.0;                             // best fitness achieved in this pass, pass is ended if it does not imporve for some amount of time
    private List<Pass> passes;                                              // list of passes in the run, used to calculate fitness of the run
    private double fitnessDeathTimer = 0.0;                                 // how much time passed since last improvement of bestFitnessInThisPass
    private double speedDeathTimer = 0.0;                                   // how much time passed since speed was not too low
    private int generationIndex = 0;                                        // index of current generation
    private int runIndex = 0;                                               // index of current run
    private int passIndex = 0;                                              // index of current pass
    private double bestRunFitness = 0.0;                                    // best fitness achieved in this simulation
    private int breakthroughGen = 0;                                        // index of generation where best fitness was achieved
    private int breakthroughRun = 0;                                        // index of run where best fitness was achieved
    private double timer = 0.0;                                             // time since start of the pass
    private double distance = 0.0;                                          // how much distance car has covered in this pass
    private double acceptedMinTime = -1.0;                                  // how fast car was able to comlete the track, should be -1 if it hasn't completed it yet
    private Vector3 previousPosition;                                       // position of the car in previous frame
    private int breakthroughCount = 0;                                      // how much fitness improvements happened with this neural network
    private bool fastForward = false;                                       // whether fast forward function is activated

    private enum RunAcceptMode
    {
        All,
        Median,
    }

    /// <summary>
    /// List of settings available to be set from StartupSettings screen.
    /// </summary>
    public static List<Setting> Settings { get; set; } = new List<Setting>()
    {
        new IntSetting("layerCount", 1),
        new IntSetting("neuronsInLayer", 2),
        new IntSetting("populationSize", 10),
        new IntSetting("passCount", 5),
        new FloatSetting("mutationPower", 3.0f),
        new FloatSetting("maxMutation", 1.0f),
        new FloatSetting("speedupTimeScale", 100.0f),
        new FloatSetting("checkpointReachDistance", 5.0f),
        new FloatSetting("randomAngleMin", -22.0f),
        new FloatSetting("randomAngleMax", 22.0f),
        new ChoiceSetting("runAcceptMode", new List<string> { "All", "Median" }, 0),
        new FloatSetting("terminationDelay", 1.0f),
        new FloatSetting("terminationSpeed", 0.5f),
        new FloatSetting("checkpointBonusWeight", 100.0f),
        new FloatSetting("distanceBonusWeight", 10.0f),
        new FloatSetting("speedBonusWeight", 1.0f),
    };

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

#pragma warning disable IDE0051 // Remove unused private members
    private void Start()
#pragma warning restore IDE0051 // Remove unused private members
    {
        // loading settings
        this.layerCount = StartupSettings.GetIntSetting("layerCount");
        this.neuronsInLayer = StartupSettings.GetIntSetting("neuronsInLayer");
        this.populationSize = StartupSettings.GetIntSetting("populationSize");
        this.passCount = StartupSettings.GetIntSetting("passCount");
        this.mutationPower = StartupSettings.GetFloatSetting("mutationPower");
        this.maxMutation = StartupSettings.GetFloatSetting("maxMutation");
        this.speedupTimeScale = StartupSettings.GetFloatSetting("speedupTimeScale");
        this.checkpointReachDistance = StartupSettings.GetFloatSetting("checkpointReachDistance");
        this.randomAngleMin = StartupSettings.GetFloatSetting("randomAngleMin");
        this.randomAngleMax = StartupSettings.GetFloatSetting("randomAngleMax");
        this.runAcceptMode = (RunAcceptMode)StartupSettings.GetChoiceSetting("runAcceptMode");
        this.terminationDelay = StartupSettings.GetFloatSetting("terminationDelay");
        this.terminationSpeed = StartupSettings.GetFloatSetting("terminationSpeed");
        this.checkpointBonusWeight = StartupSettings.GetFloatSetting("checkpointBonusWeight");
        this.distanceBonusWeight = StartupSettings.GetFloatSetting("distanceBonusWeight");
        this.speedBonusWeight = StartupSettings.GetFloatSetting("speedBonusWeight");

        // getting CarController
        this.carController = this.carObject.GetComponent<CarController>();

        // loading track
        Transform tracksParent = GameObject.Find("Tracks").transform;
        foreach (Transform t in tracksParent)
        {
            t.gameObject.SetActive(false);
        }
        this.track = tracksParent.GetChild(StartupSettings.trackIndex);
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
        if (StartupSettings.neuralNetwork != null)
        {
            this.bestNetwork = StartupSettings.neuralNetwork;
            this.breakthroughCount = this.bestNetwork.breakthroughCount;
            if (!StartupSettings.resetFitness)
            {
                this.bestRunFitness = this.bestNetwork.fitness;
            }
        }
        else
        {
            this.bestNetwork = new NeuralNetwork(StartupSettings.registeredInputs, StartupSettings.registeredOutputs, this.layerCount, this.neuronsInLayer);
        }

        // preparing simulation
        this.PreGeneration();
        this.PreRun();
        this.PrePass();
    }

#pragma warning disable IDE0051 // Remove unused private members
    private void Update()
#pragma warning restore IDE0051 // Remove unused private members
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
                Time.timeScale = this.speedupTimeScale;
            }
            this.fastForward = !this.fastForward;
        }
    }

#pragma warning disable IDE0051 // Remove unused private members
    private void FixedUpdate()
#pragma warning restore IDE0051 // Remove unused private members
    {
        this.distance += Vector3.Distance(this.carObject.transform.position, this.previousPosition);
        this.previousPosition = this.carObject.transform.position;
        this.timer += Time.fixedDeltaTime;
        if (this.CheckDeathConditions())
        {
            return;
        }

        this.UpdateDeathTimers();

        this.AddBonuses();

        this.UpdateUIText();
    }

    private bool CheckDeathConditions()
    {
        if (this.fitnessDeathTimer > this.terminationDelay || this.speedDeathTimer > this.terminationDelay || this.carObject.transform.position.y < 0.0f || this.CollisionDetected || this.NextCheckpoint >= this.checkpoints.Count)
        {
            if (this.fitnessDeathTimer > this.terminationDelay)
            {
                Debug.Log("FITNESS Max: " + this.bestFitnessInThisPass + " Current: " + this.PassFitness);
            }
            if (this.speedDeathTimer > this.terminationDelay)
            {
                Debug.Log("SPEED");
            }
            if (this.carObject.transform.position.y < 0.0f)
            {
                Debug.Log("FALL");
            }
            if (this.CollisionDetected)
            {
                Debug.Log("COLLISION");
            }
            if (this.NextCheckpoint >= this.checkpoints.Count)
            {
                Debug.Log("FINISH");
            }
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
            this.fitnessDeathTimer = 0.0;
            this.bestFitnessInThisPass = this.PassFitness;
        }

        // speed timer
        double currentCarVelocity = this.carObject.GetComponent<Rigidbody>().velocity.magnitude;
        if (currentCarVelocity < this.terminationSpeed)
        {
            this.speedDeathTimer += Time.fixedDeltaTime;
        }
        else
        {
            this.speedDeathTimer = 0.0;
        }
    }

    private void AddBonuses()
    {
        // has to update next checkpoint first
        if (this.NextCheckpoint < this.checkpoints.Count)
        {
            if (Vector3.Distance(this.carObject.transform.position, this.checkpoints[this.NextCheckpoint].position) < this.checkpointReachDistance)
            {
                this.NextCheckpoint++;
            }
        }

        // distance bonus
        double distanceBonus = 0.0;
        if (this.NextCheckpoint < this.checkpoints.Count)
        {
            float distanceToNextCheckpoint = Vector3.Distance(this.carObject.transform.position, this.checkpoints[this.NextCheckpoint].position);
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * this.distanceBonusWeight;
        }

        // checkpoint bonus
        double checkpointBonus = this.NextCheckpoint * this.checkpointBonusWeight;

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
        this.timeText.text = "TIME: " + this.timer;
        this.speedDeathTimerText.text = string.Format("SPD: {0:0.0}", this.speedDeathTimer);
        this.fitnessDeathTimerText.text = string.Format("FIT: {0:0.0}", this.fitnessDeathTimer);
    }

    private void CheckBestResult(double runFitness, double runMinTime)
    {
        // has to be in here so it will be saved in the file
        this.Generation[this.runIndex].fitness = runFitness;

        // updating fitness and best results
        // if it is same neural network (run 0), result is not accepted, except if it is first update
        if (runFitness > this.bestRunFitness && (this.runIndex > 0 || this.bestRunFitness == 0.0))
        {
            // new breakthrough, new breakthough count
            this.breakthroughCount++;
            this.Generation[this.runIndex].breakthroughCount = this.breakthroughCount;

            // updating index of best run
            this.bestRunFitness = runFitness;
            this.breakthroughGen = this.generationIndex;
            this.breakthroughRun = this.runIndex;

            // saving best neural network to file
            string trackName = this.track.name.Replace(" ", string.Empty);
            string dateString = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string bcString = "bc" + this.breakthroughCount;
            string genRunString = "g" + this.generationIndex + "r" + this.runIndex;
            string filePath = trackName + "_" + dateString + "_" + bcString + "_" + genRunString + ".txt";
            Directory.CreateDirectory(this.networksFolderPath);
            this.Generation[this.runIndex].SaveToFile(StartupSettings.networksFolderPath + "/" + filePath);
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
        this.Generation.Sort((x, y) => -x.fitness.CompareTo(y.fitness));

        // if we have new best result
        if (this.Generation[0].fitness > this.bestNetwork.fitness)
        {
            this.bestNetwork = this.Generation[0];
        }
    }

    // called just after starting new generation
    private void PreGeneration()
    {
        // creating new neural networks
        List<NeuralNetwork> newGeneration = new List<NeuralNetwork>();
        for (int i = 0; i < this.populationSize; i++)
        {
            // WARNING: results of the run 0 are not counted, so if you will make first network in generation mutate, make results of the run 0 count
            NeuralNetwork newNetwork = this.bestNetwork.Copy();
            newNetwork.Mutate(1, this.maxMutation * Math.Pow((double)i / this.populationSize, this.mutationPower));
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

        double runMinTime = -1.0;
        double runFitness = 0.0;

        // if mode is Median, we take median pass
        if (this.runAcceptMode == RunAcceptMode.Median)
        {
            Pass medianPass = this.passes[(this.passes.Count - 1) / 2];
            runFitness = medianPass.Fitness;
            if (medianPass.NextCheckpoint >= this.checkpoints.Count)
            {
                runMinTime = medianPass.Time;
            }
        }

        // if it is All, we take the worst pass
        else if (this.runAcceptMode == RunAcceptMode.All)
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
        this.carController.neuralNetwork = network;

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
            double averageSpeed = this.distance / this.timer;
            speedBonus = Math.Tanh(averageSpeed * this.speedBonusWeight);
            timeBonus = 0.0;
        }
        else
        {
            speedBonus = 0.0;
            timeBonus = 1.0 / (this.timer + 1.0);
        }
        double savedFitness = this.PassFitness;
        this.PassFitness += speedBonus;
        this.PassFitness += timeBonus;

        // adding this pass to the list
        Pass pass = new Pass
        {
            Fitness = this.PassFitness,
            Time = this.timer,
            NextCheckpoint = this.NextCheckpoint,
        };
        this.passes.Add(pass);

        // distance bonus (not added to passFitness, used only for debug)
        double distanceBonus = 0.0;
        if (this.NextCheckpoint < this.checkpoints.Count)
        {
            float distanceToNextCheckpoint = Vector3.Distance(this.carObject.transform.position, this.checkpoints[this.NextCheckpoint].position);
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * 10.0;
        }

        // checkpoint bonus (not added to pass fitness, used only for debug)
        double checkpointBonus = this.NextCheckpoint * 100.0;

        Debug.Log("Pass fitness: " + this.PassFitness + " Nsb: " + savedFitness + " Time: " + this.timer + " Distance: " + this.distance + " Avg sp: " + (this.distance / this.timer));
        Debug.Log("Chk: " + checkpointBonus + " Dst: " + distanceBonus + " Spd: " + speedBonus + " T: " + timeBonus);

        // if car was not able to improve best result, and we take the worst pass in the run as fitness of the whole run, there is no point in continuing this run
        if (this.runAcceptMode == RunAcceptMode.All && this.PassFitness < this.bestRunFitness)
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
        this.fitnessDeathTimer = 0.0;
        this.speedDeathTimer = 0.0;
        this.bestFitnessInThisPass = 0.0;
        this.distance = 0.0;
        this.previousPosition = this.carSpawnPoint.transform.position;
        this.timer = 0.0;
        this.NextCheckpoint = 0;
        this.CollisionDetected = false;
        this.PassFitness = 0;
        this.carController.ResetQueues();

        // randomized rotation
        this.carObject.transform.rotation *= Quaternion.Euler(Vector3.up * (float)Utils.MapRange(this.passIndex, 0, this.passCount - 1, this.randomAngleMin, this.randomAngleMax));

        Debug.Log("Generation " + this.generationIndex + " Car: " + this.runIndex + " Pass: " + this.passIndex + " Max: " + this.bestRunFitness + " Gen: " + this.breakthroughGen + " Car: " + this.breakthroughRun);
    }

    // proceed to the next run
    private void NextRun()
    {
        this.PostRun();

        this.runIndex++;

        if (this.runIndex > this.populationSize - 1)
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

        if (this.passIndex > this.passCount - 1 || runAborted)
        {
            this.NextRun();
        }

        this.PrePass();
    }

    // this is needed to calculate fitness of the run
    private struct Pass
    {
        public double Fitness;
        public double Time;
        public double NextCheckpoint;
    }
}
