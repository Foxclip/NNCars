using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{

    public const int INPUT_COUNT = 11;

    public int layerCount = 1;
    public int neuronsInLayer = 16;
    public int populationSize = 10;
    public int passCount = 3;
    public float crossoverPower = 2;
    public float mutationPower = 10;
    public float maxMutation = 1;
    public float speedupTimeScale = 10;
    public float checkpointReachDistance = 3.0f;
    public double randomAngleMin = -25.0;
    public double randomAngleMax = 25.0;
    public enum RunAcceptMode
    {
        All,
        Median
    };
    public RunAcceptMode runAcceptMode;
    public bool loadNetwork = true;

    public GameObject carObject;
    public Transform track;
    public double terminationDelay = 1.0;
    public double terminationSpeed = 0.2;
    public double checkpointBonusWeight = 100.0;
    public double distanceBonusWeight = 10.0;
    public double speedBonusWeight = 0.01;

    public string saveFolderPath = "Networks/";

    public Text genRunPassText;
    public Text passFitnessText;
    public Text maxFitnessText;
    public Text bestCarText;
    public Text minTimeText;
    public Text timeText;
    public Text speedDeathTimerText;
    public Text fitnessDeathTimerText;

    [HideInInspector]
    public List<NeuralNetwork> generation = new List<NeuralNetwork>();
    [HideInInspector]
    public bool collisionDetected = false;
    [HideInInspector]
    public int nextCheckpoint = 0;
    [HideInInspector]
    public double passFitness;

    private struct Pass
    {
        public double fitness;
        public double time;
        public double nextCheckpoint;
    };

    private Transform carSpawnPoint;
    private List<Transform> checkpoints = new List<Transform>();
    private NeuralNetwork bestNetwork;
    private double bestFitnessInThisPass = 0.0;
    private List<Pass> passes;
    private double fitnessDeathTimer = 0.0;
    private double speedDeathTimer = 0.0;
    private int generationIndex = 0;
    private int runIndex = 0;
    private int passIndex = 0;
    private double bestRunFitness = 0.0;
    private int breakthroughGen = 0;
    private int breakthroughRun = 0;
    private double timer = 0.0;
    private double distance = 0.0;
    private double acceptedMinTime = -1.0;
    private Vector3 previousPosition;

    private bool fastForward = false;

    void Start()
    {

        //loading spawn point
        carSpawnPoint = track.Find("Spawn");

        //loading checkpoints
        Transform checkpointsParent = track.Find("Checkpoints");
        foreach (Transform child in checkpointsParent)
        {
            checkpoints.Add(child);
        }

        //initial neural network
        if (loadNetwork)
        {
            bestNetwork = NeuralNetwork.Deserialize("network.xml");
        }
        else
        {
            bestNetwork = new NeuralNetwork(INPUT_COUNT, layerCount, neuronsInLayer);
        }

        PreGeneration();
        PreRun();
        PrePass();

    }

    void Update()
    {
        if (Input.GetKeyDown("space"))
        {
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
        timeText.text = "TIME: " + timer;
        speedDeathTimerText.text = String.Format("SPD: {0:0.0}", speedDeathTimer);
        fitnessDeathTimerText.text = String.Format("FIT: {0:0.0}", fitnessDeathTimer);
    }

    void PostGeneration()
    {

        generation.Sort((x, y) => -x.fitness.CompareTo(y.fitness));

        //if we have new best result
        if (generation[0].fitness > bestNetwork.fitness)
        {
            bestNetwork = generation[0];
        }

    }

    void PreGeneration()
    {

        List<NeuralNetwork> newGeneration = new List<NeuralNetwork>();

        for (int i = 0; i < populationSize; i++)
        {

            NeuralNetwork newNetwork = NeuralNetwork.Crossover(bestNetwork, bestNetwork);

            newNetwork.Mutate(1, maxMutation * Math.Pow((double)i / populationSize, mutationPower));
            newGeneration.Add(newNetwork);

        }

        generation = new List<NeuralNetwork>(newGeneration);

        runIndex = 0;

    }

    void NextGeneration()
    {

        PostGeneration();

        generationIndex++;

        PreGeneration();

    }

    void PostRun()
    {

        //calculating fitness of the run

        double runMinTime = -1.0;
        double runFitness = 0.0;

        if (runAcceptMode == RunAcceptMode.Median)
        {
            Pass medianPass = passes[(passes.Count - 1) / 2];
            runFitness = medianPass.fitness;
            if(medianPass.nextCheckpoint >= checkpoints.Count)
            {
                runMinTime = medianPass.time;
            }
        }
        else if (runAcceptMode == RunAcceptMode.All)
        {
            passes.Sort((x, y) => x.fitness.CompareTo(y.fitness));
            runFitness = passes[0].fitness;
            if(passes[0].nextCheckpoint >= checkpoints.Count)
            {
                runMinTime = passes[0].time;
            }
        }

        //has to be in here so it will be saved in the file
        generation[runIndex].fitness = runFitness;

        //updating fitness and best results
        if (runFitness > bestRunFitness)
        {

            //updating index of best run
            bestRunFitness = runFitness;
            breakthroughGen = generationIndex;
            breakthroughRun = runIndex;

            //saving best neural network to file
            String trackName = track.name.Replace(' ', '_');
            String genRunString = "g" + generationIndex + "r" + runIndex;
            String dateString = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            generation[runIndex].Serialize(saveFolderPath + trackName + "_" + dateString + "_" + genRunString + ".xml");

        }

        //updating best time
        if(runMinTime >= 0.0 && (runMinTime < acceptedMinTime || acceptedMinTime < 0.0))
        {
            acceptedMinTime = runMinTime;
        }

    }

    void PreRun()
    {

        //initializing neural network
        NeuralNetwork network = generation[runIndex];
        carObject.GetComponent<CarController>().neuralNetwork = network;

        passes = new List<Pass>();

        passIndex = 0;

    }

    bool PostPass()
    {

        //speed and time bonuses
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

        //distance bonus (not added to passFitness)
        double distanceBonus = 0.0;
        if (nextCheckpoint < checkpoints.Count)
        {
            float distanceToNextCheckpoint = Vector3.Distance(carObject.transform.position, checkpoints[nextCheckpoint].position);
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * 10.0;
        }
        //checkpoint bonus (not added to pass fitness)
        double checkpointBonus = nextCheckpoint * 100.0;

        Debug.Log("Pass fitness: " + passFitness + " Nsb: " + savedFitness + " Time: " + timer + " Distance: " + distance + " Avg sp: " + distance / timer);
        Debug.Log("Chk: " + checkpointBonus + " Dst: " + distanceBonus + " Spd: " + speedBonus + " T: " + timeBonus);

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
        carObject.GetComponent<CarController>().ResetQueues();

        //randomized rotation
        //carObject.transform.rotation *= Quaternion.Euler(Vector3.up * (float)Utils.RandBetween(-45, 45));
        carObject.transform.rotation *= Quaternion.Euler(Vector3.up * (float)Utils.MapRange(passIndex, 0, passCount - 1, randomAngleMin, randomAngleMax));

        Debug.Log("Generation " + generationIndex + " Car: " + runIndex + " Pass: " + passIndex + " Max: " + bestRunFitness + " Gen: " + breakthroughGen + " Car: " + breakthroughRun);

    }

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
