using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{

    //list of variables which can be set from StartupSettings
    public static List<Setting> settings = new List<Setting>() {
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
        new FloatSetting("speedBonusWeight", 1.0f)
    };

    public int layerCount = 1;                      //number of hidden layers neural network will have
    public int neuronsInLayer = 16;                 //number of neurons in hidden layers
    public int populationSize = 10;                 //number of diffrerent neural networks in generation
    public int passCount = 3;                       //to improve stability of the solution, several passes are made for each neural network
    public float mutationPower = 10;                //how likely small mutations are
    public float maxMutation = 1;                   //maximal amount of mutation in generation
    public float speedupTimeScale = 10;             //when pressing Space, simulation will speed up
    public float checkpointReachDistance = 3.0f;    //checkpoint is counted as reached when car is within this distance
    public double randomAngleMin = -25.0;           //minimal angle of random rotation in the beginning of each pass
    public double randomAngleMax = 25.0;            //maximal angle
    public enum RunAcceptMode
    {
        All,
        Median
    };
    public RunAcceptMode runAcceptMode;             //determines how fitness of the run is calculated

    public GameObject carObject;                    //GameObject of the car
    public double terminationDelay = 1.0;           //pass is ended if car's speed is below termination speed or fitness does not improve for this amount of time
    public double terminationSpeed = 0.2;           //what speed is too low
    public double checkpointBonusWeight = 100.0;    //weight of the checkpoint bonus
    public double distanceBonusWeight = 10.0;       //weight of the distance bonus
    public double speedBonusWeight = 0.01;          //weight of the speed bonus

    public string networksFolderPath = "Networks/"; //folder which neural networks are saved to

    //UI text
    public Text genRunPassText;
    public Text passFitnessText;
    public Text maxFitnessText;
    public Text bestCarText;
    public Text minTimeText;
    public Text breakthroughCountText;
    public Text timeText;
    public Text speedDeathTimerText;
    public Text fitnessDeathTimerText;

    [HideInInspector]
    public List<NeuralNetwork> generation = new List<NeuralNetwork>();  //current generation of neural networks
    [HideInInspector]
    public bool collisionDetected = false;                              //car collided with a wall
    [HideInInspector]
    public int nextCheckpoint = 0;                                      //index of the next checkpoint
    [HideInInspector]
    public double passFitness;                                          //fitness of the current pass

    //this is needed to calculate fitness of the run
    private struct Pass
    {
        public double fitness;
        public double time;
        public double nextCheckpoint;
    };

    private CarController carController;                            //CarController script
    private Transform track;                                        //transform of the track
    private Transform carSpawnPoint;                                //where car will be placed before strting a pass
    private List<Transform> checkpoints = new List<Transform>();    //list of all checkpoints in the track
    private NeuralNetwork bestNetwork;                              //best result of the simulation
    private double bestFitnessInThisPass = 0.0;                     //best fitness achieved in this pass, pass is ended if it does not imporve for some amount of time
    private List<Pass> passes;                                      //list of passes in the run, used to calculate fitness of the run
    private double fitnessDeathTimer = 0.0;                         //how much time passed since last improvement of bestFitnessInThisPass
    private double speedDeathTimer = 0.0;                           //how much time passed since speed was not too low
    private int generationIndex = 0;                                //index of current generation
    private int runIndex = 0;                                       //index of current run
    private int passIndex = 0;                                      //index of current pass
    private double bestRunFitness = 0.0;                            //best fitness achieved in this simulation
    private int breakthroughGen = 0;                                //index of generation where best fitness was achieved
    private int breakthroughRun = 0;                                //index of run where best fitness was achieved
    private double timer = 0.0;                                     //time since start of the pass
    private double distance = 0.0;                                  //how much distance car has covered in this pass
    private double acceptedMinTime = -1.0;                          //how fast car was able to comlete the track, should be -1 if it hasn't completed it yet
    private Vector3 previousPosition;                               //position of the car in previous frame
    private int breakthroughCount = 0;                              //how much fitness improvements happened with this neural network

    private bool fastForward = false;                               //whether fast forward function is activated

    void Start()
    {

        //getting CarController
        carController = carObject.GetComponent<CarController>();

        //loading track
        Transform tracksParent = GameObject.Find("Tracks").transform;
        foreach(Transform t in tracksParent)
        {
            t.gameObject.SetActive(false);
        }
        track = tracksParent.GetChild(StartupSettings.trackIndex);
        track.gameObject.SetActive(true);

        //loading spawn point
        carSpawnPoint = track.Find("Spawn");

        //loading checkpoints
        Transform checkpointsParent = track.Find("Checkpoints");
        foreach (Transform child in checkpointsParent)
        {
            checkpoints.Add(child);
        }

        //loading neural network or creating new one
        if (StartupSettings.neuralNetwork != null)
        {
            bestNetwork = StartupSettings.neuralNetwork;
            breakthroughCount = bestNetwork.breakthroughCount;
            if (!StartupSettings.resetFitness)
            {
                bestRunFitness = bestNetwork.fitness;
            }
        }
        else
        {
            bestNetwork = new NeuralNetwork(StartupSettings.registeredInputs, StartupSettings.registeredOutputs, layerCount, neuronsInLayer);
        }

        //preparing simulation
        PreGeneration();
        PreRun();
        PrePass();

    }

    void Update()
    {
        if (Input.GetKeyDown("space"))
        {
            //fast forward function
            if (fastForward)
            {
                Time.timeScale = 1.0f;
            } else
            {
                Time.timeScale = speedupTimeScale;
            }
            fastForward = !fastForward;
        }
    }

    void FixedUpdate()
    {

        distance += Vector3.Distance(carObject.transform.position, previousPosition);
        previousPosition = carObject.transform.position;
        timer += Time.fixedDeltaTime;

        if(CheckDeathConditions())
        {
            return;
        }

        UpdateDeathTimers();

        AddBonuses();

        UpdateUIText();

    }

    bool CheckDeathConditions()
    {
        if (fitnessDeathTimer > terminationDelay || speedDeathTimer > terminationDelay || carObject.transform.position.y < 0.0f || collisionDetected || nextCheckpoint >= checkpoints.Count)
        {
            if (fitnessDeathTimer > terminationDelay)
            {
                Debug.Log("FITNESS Max: " + bestFitnessInThisPass + " Current: " + passFitness);
            }
            if (speedDeathTimer > terminationDelay)
            {
                Debug.Log("SPEED");
            }
            if (carObject.transform.position.y < 0.0f)
            {
                Debug.Log("FALL");
            }
            if (collisionDetected)
            {
                Debug.Log("COLLISION");
            }
            if(nextCheckpoint >= checkpoints.Count)
            {
                Debug.Log("FINISH");
            }
            NextPass();
            return true;
        }
        return false;
    }

    void UpdateDeathTimers()
    {

        //fitness timer
        if (passFitness <= bestFitnessInThisPass)
        {
            fitnessDeathTimer += Time.fixedDeltaTime;
        }
        else
        {
            fitnessDeathTimer = 0.0;
            bestFitnessInThisPass = passFitness;
        }

        //speed timer
        double currentCarVelocity = carObject.GetComponent<Rigidbody>().velocity.magnitude;
        if (currentCarVelocity < terminationSpeed)
        {
            speedDeathTimer += Time.fixedDeltaTime;
        }
        else
        {
            speedDeathTimer = 0.0;
        }

    }

    void AddBonuses()
    {

        //has to update next checkpoint first
        if (nextCheckpoint < checkpoints.Count)
        {
            if (Vector3.Distance(carObject.transform.position, checkpoints[nextCheckpoint].position) < checkpointReachDistance)
            {
                nextCheckpoint++;
            }
        }

        //distance bonus
        double distanceBonus = 0.0;
        if (nextCheckpoint < checkpoints.Count)
        {
            float distanceToNextCheckpoint = Vector3.Distance(carObject.transform.position, checkpoints[nextCheckpoint].position);
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * distanceBonusWeight;
        }

        //checkpoint bonus
        double checkpointBonus = nextCheckpoint * checkpointBonusWeight;

        passFitness = checkpointBonus + distanceBonus;

    }

    void UpdateUIText()
    {
        genRunPassText.text = "GEN " + generationIndex + " RUN " + runIndex + " PASS " + passIndex;
        passFitnessText.text = "PASS FITNESS: " + passFitness;
        maxFitnessText.text = "MAX FITNESS: " + bestRunFitness;
        bestCarText.text = "BEST: GEN " + breakthroughGen + " RUN " + breakthroughRun;
        minTimeText.text = "MIN TIME: " + acceptedMinTime;
        breakthroughCountText.text = "BREAKTHROUGHS: " + breakthroughCount;
        timeText.text = "TIME: " + timer;
        speedDeathTimerText.text = String.Format("SPD: {0:0.0}", speedDeathTimer);
        fitnessDeathTimerText.text = String.Format("FIT: {0:0.0}", fitnessDeathTimer);
    }

    void CheckBestResult(double runFitness, double runMinTime)
    {
        //has to be in here so it will be saved in the file
        generation[runIndex].fitness = runFitness;

        //updating fitness and best results
        //if it is same neural network (run 0), result is not accepted, except if it is first update
        if (runFitness > bestRunFitness && (runIndex > 0 || bestRunFitness == 0.0))
        {

            //new breakthrough, new breakthough count
            breakthroughCount++;
            generation[runIndex].breakthroughCount = breakthroughCount;

            //updating index of best run
            bestRunFitness = runFitness;
            breakthroughGen = generationIndex;
            breakthroughRun = runIndex;

            //saving best neural network to file
            string trackName = track.name.Replace(" ", "");
            string dateString = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string bcString = "bc" + breakthroughCount;
            string genRunString = "g" + generationIndex + "r" + runIndex;
            string filePath = trackName + "_" + dateString + "_" + bcString + "_" + genRunString + ".txt";
            Directory.CreateDirectory(networksFolderPath);
            generation[runIndex].SaveToFile(StartupSettings.networksFolderPath + "/" + filePath);

        }

        //updating best time
        //if run 0 improves time, it will be updated, but max fitness will not
        if (runMinTime >= 0.0 && (runMinTime < acceptedMinTime || acceptedMinTime < 0.0))
        {
            acceptedMinTime = runMinTime;
        }

    }

    //is called after generation is complete
    void PostGeneration()
    {

        generation.Sort((x, y) => -x.fitness.CompareTo(y.fitness));

        //if we have new best result
        if (generation[0].fitness > bestNetwork.fitness)
        {
            bestNetwork = generation[0];
        }

    }

    //called just after starting new generation
    void PreGeneration()
    {

        //creating new neural networks
        List<NeuralNetwork> newGeneration = new List<NeuralNetwork>();
        for (int i = 0; i < populationSize; i++)
        {
            //WARNING: results of the run 0 are not counted, so if you will make first network in generation mutate, make results of the run 0 count
            NeuralNetwork newNetwork = bestNetwork.Copy();
            newNetwork.Mutate(1, maxMutation * Math.Pow((double)i / populationSize, mutationPower));
            newGeneration.Add(newNetwork);
        }

        //swapping generations
        generation = new List<NeuralNetwork>(newGeneration);

        runIndex = 0;

    }

    //proceed to the next generation
    void NextGeneration()
    {

        PostGeneration();

        generationIndex++;

        PreGeneration();

    }

    //called after run is complete
    void PostRun()
    {

        //calculating fitness of the run

        double runMinTime = -1.0;
        double runFitness = 0.0;

        //if mode is Median, we take median pass
        if (runAcceptMode == RunAcceptMode.Median)
        {
            Pass medianPass = passes[(passes.Count - 1) / 2];
            runFitness = medianPass.fitness;
            if(medianPass.nextCheckpoint >= checkpoints.Count)
            {
                runMinTime = medianPass.time;
            }
        }
        //if it is All, we take the worst pass
        else if (runAcceptMode == RunAcceptMode.All)
        {
            passes.Sort((x, y) => x.fitness.CompareTo(y.fitness));
            runFitness = passes[0].fitness;
            if(passes[0].nextCheckpoint >= checkpoints.Count)
            {
                runMinTime = passes[0].time;
            }
        }

        //check if result of this run is the best one
        CheckBestResult(runFitness, runMinTime);

    }

    //called just after starting new run
    void PreRun()
    {

        //initializing neural network
        NeuralNetwork network = generation[runIndex];
        carController.neuralNetwork = network;

        //list of passes has to be cleared
        passes = new List<Pass>();

        passIndex = 0;

    }

    //called after completing a pass
    bool PostPass()
    {

        //speed and time bonuses
        //if the car completes the track, it gets time bonus
        //otherwise, it gets speed bonus
        double speedBonus = 0.0;
        double timeBonus = 0.0;
        if (nextCheckpoint < checkpoints.Count)
        {
            double averageSpeed = distance / timer;
            speedBonus = Math.Tanh(averageSpeed * speedBonusWeight);
            timeBonus = 0.0;
        }
        else
        {
            speedBonus = 0.0;
            timeBonus = 1.0 / (timer + 1.0);
        }
        double savedFitness = passFitness;
        passFitness += speedBonus;
        passFitness += timeBonus;

        //adding this pass to the list
        Pass pass = new Pass();
        pass.fitness = passFitness;
        pass.time = timer;
        pass.nextCheckpoint = nextCheckpoint;
        passes.Add(pass);

        //distance bonus (not added to passFitness, used only for debug)
        double distanceBonus = 0.0;
        if (nextCheckpoint < checkpoints.Count)
        {
            float distanceToNextCheckpoint = Vector3.Distance(carObject.transform.position, checkpoints[nextCheckpoint].position);
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * 10.0;
        }
        //checkpoint bonus (not added to pass fitness, used only for debug)
        double checkpointBonus = nextCheckpoint * 100.0;

        Debug.Log("Pass fitness: " + passFitness + " Nsb: " + savedFitness + " Time: " + timer + " Distance: " + distance + " Avg sp: " + distance / timer);
        Debug.Log("Chk: " + checkpointBonus + " Dst: " + distanceBonus + " Spd: " + speedBonus + " T: " + timeBonus);

        //if car was not able to improve best result, and we take the worst pass in the run as fitness of the whole run, there is no point in continuing this run
        if(runAcceptMode == RunAcceptMode.All && passFitness < bestRunFitness)
        {
            return true;
        }

        return false;

    }

    void PrePass()
    {
        //resetting car parameters
        carObject.transform.position = carSpawnPoint.transform.position;
        carObject.transform.rotation = carSpawnPoint.transform.rotation;
        carObject.GetComponent<Rigidbody>().velocity = new Vector3(0.0f, 0.0f, 0.0f);
        carObject.GetComponent<Rigidbody>().angularVelocity = new Vector3(0.0f, 0.0f, 0.0f);
        fitnessDeathTimer = 0.0;
        speedDeathTimer = 0.0;
        bestFitnessInThisPass = 0.0;
        distance = 0.0;
        previousPosition = carSpawnPoint.transform.position;
        timer = 0.0;
        nextCheckpoint = 0;
        collisionDetected = false;
        passFitness = 0;
        carController.ResetQueues();

        //randomized rotation
        carObject.transform.rotation *= Quaternion.Euler(Vector3.up * (float)Utils.MapRange(passIndex, 0, passCount - 1, randomAngleMin, randomAngleMax));

        Debug.Log("Generation " + generationIndex + " Car: " + runIndex + " Pass: " + passIndex + " Max: " + bestRunFitness + " Gen: " + breakthroughGen + " Car: " + breakthroughRun);

    }

    //proceed to the next run
    void NextRun()
    {

        PostRun();

        runIndex++;

        if (runIndex > populationSize - 1)
        {
            NextGeneration();
        }

        PreRun();

    }

    //proceed to the next pass
    //notice that run can be aborted early
    void NextPass()
    {

        bool runAborted = PostPass();

        passIndex++;

        if(passIndex > passCount - 1 || runAborted)
        {
            NextRun();
        }

        PrePass();
    }

}
