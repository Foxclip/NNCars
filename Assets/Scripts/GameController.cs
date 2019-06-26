using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{

    private const int INPUT_COUNT = 11;

    public int layerCount = 1;
    public int neuronsInLayer = 16;
    public int populationSize = 10;
    public float crossoverPower = 2;
    public float mutationPower = 10;
    public float maxMutation = 1;
    public float speedupTimeScale = 10;
    public float checkpointReachDistance = 3.0f;

    public GameObject carObject;
    public GameObject carSpawnPoint;
    public GameObject checkpointsParent;
    public double terminationDelay = 1.0;
    public double terminationSpeed = 0.2;

    public Text generationText;
    public Text carText;
    public Text currentFitnessText;
    public Text maxFitnessText;
    public Text bestCarText;
    public Text minTimeText;
    public Text timeText;

    [HideInInspector]
    public List<NeuralNetwork> generation = new List<NeuralNetwork>();
    [HideInInspector]
    public bool collisionDetected = false;
    [HideInInspector]
    public int nextCheckpoint = 0;

    private List<Transform> checkpoints = new List<Transform>();
    private NeuralNetwork bestNetwork;
    private double bestFitnessInThisRun = 0.0;
    private double fitnessDeathTimer = 0.0;
    private double speedDeathTimer = 0.0;
    private int generationIndex = 0;
    private int generationMemberIndex = -1;
    private double totalBestFitness = 0.0;
    private int breakthroughGen = 0;
    private int breakthroughCar = 0;
    private double timer = 0.0;
    private double distance = 0.0;
    private double minTime = -1.0;
    private Vector3 previousPosition;

    private bool fastForward = false;

    void Start()
    {

        //creating initial population
        for (int i = 0; i < populationSize; i++)
        {
            NeuralNetwork newNetwork = new NeuralNetwork(INPUT_COUNT, layerCount, neuronsInLayer);
            newNetwork.Mutate(1, maxMutation * Math.Pow((double)i / populationSize, mutationPower));
            generation.Add(newNetwork);
        }
        bestNetwork = generation[0];

        previousPosition = carSpawnPoint.transform.position;

        //loading checkpoints
        foreach (Transform child in checkpointsParent.transform)
        {
            checkpoints.Add(child);
        }

        NextCar();

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

        CheckDeathConditions();

        UpdateDeathTimers();

        AddBonuses();

        UpdateUIText();

    }

    void CheckDeathConditions()
    {
        if (fitnessDeathTimer > terminationDelay || speedDeathTimer > terminationDelay || carObject.transform.position.y < 0.0f || collisionDetected)
        {
            if (fitnessDeathTimer > terminationDelay)
            {
                double currentFitness = generation[generationMemberIndex].fitness;
                Debug.Log("FITNESS Max: " + bestFitnessInThisRun + " Current: " + currentFitness);
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
            NextCar();
            return;
        }
    }

    void UpdateDeathTimers()
    {

        //fitness timer
        double currentFitness = generation[generationMemberIndex].fitness;
        if (currentFitness <= bestFitnessInThisRun)
        {
            fitnessDeathTimer += Time.fixedDeltaTime;
        }
        else
        {
            fitnessDeathTimer = 0.0;
            bestFitnessInThisRun = currentFitness;
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
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * 10.0;
        }

        //checkpoint bonus
        double checkpointBonus = nextCheckpoint * 100.0;

        generation[generationMemberIndex].fitness = checkpointBonus + distanceBonus;
    }

    void UpdateUIText()
    {
        generationText.text = "GENERATION: " + generationIndex;
        carText.text = "CAR: " + generationMemberIndex;
        currentFitnessText.text = "FITNESS: " + generation[generationMemberIndex].fitness;
        maxFitnessText.text = "MAX FITNESS: " + totalBestFitness;
        bestCarText.text = "BEST: GEN " + breakthroughGen + " CAR " + breakthroughCar;
        minTimeText.text = "MIN TIME: " + minTime;
        timeText.text = "TIME: " + timer;
    }

    void PostRun()
    {

        //distance bonus
        double distanceBonus = 0.0;
        if (nextCheckpoint < checkpoints.Count)
        {
            float distanceToNextCheckpoint = Vector3.Distance(carObject.transform.position, checkpoints[nextCheckpoint].position);
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * 10.0;
        }

        //checkpoint bonus
        double checkpointBonus = nextCheckpoint * 100.0;

        //speed and time bonuses
        double speedBonus = 0.0;
        double timeBonus = 0.0;
        if (nextCheckpoint < checkpoints.Count)
        {
            double averageSpeed = distance / timer;
            speedBonus = Math.Tanh(averageSpeed / 100.0);
            timeBonus = 0.0;
        }
        else
        {
            speedBonus = 0.0;
            timeBonus = 1.0 / (timer + 1.0);
        }
        double savedFitness = generation[generationMemberIndex].fitness;
        generation[generationMemberIndex].fitness += speedBonus;
        generation[generationMemberIndex].fitness += timeBonus;

        //updating fitness and best results
        double fitness = generation[generationMemberIndex].fitness;
        if (fitness > totalBestFitness)
        {
            totalBestFitness = fitness;
            breakthroughGen = generationIndex;
            breakthroughCar = generationMemberIndex;
        }

        //updating minimal time
        if (nextCheckpoint >= checkpoints.Count)
        {
            if (timer < minTime || minTime < 0)
            {
                minTime = timer;
            }
        }

        Debug.Log("Fitness: " + generation[generationMemberIndex].fitness + " Nsb: " + savedFitness + " Time: " + timer + " Distance: " + distance + " Avg sp: " + distance / timer);
        Debug.Log("Chk: " + checkpointBonus + " Dst: " + distanceBonus + " Spd: " + speedBonus + " T: " + timeBonus);
    }

    void NextGeneration()
    {
        generation.Sort((x, y) => -x.fitness.CompareTo(y.fitness));

        List<NeuralNetwork> newGeneration = new List<NeuralNetwork>();

        for (int i = 0; i < populationSize; i++)
        {

            //if we have new best result
            if (generation[0].fitness > bestNetwork.fitness)
            {
                bestNetwork = generation[0];
            }

            NeuralNetwork newNetwork = NeuralNetwork.Crossover(bestNetwork, bestNetwork);

            newNetwork.Mutate(1, maxMutation * Math.Pow((double)i / populationSize, mutationPower));
            newGeneration.Add(newNetwork);

        }

        generation = new List<NeuralNetwork>(newGeneration);

        generationIndex++;
        generationMemberIndex = 0;
    }

    void PreRun()
    {

        //initializing neural network
        NeuralNetwork network = generation[generationMemberIndex];
        carObject.GetComponent<CarController>().neuralNetwork = network;

        //initializing car parameters
        carObject.transform.position = carSpawnPoint.transform.position;
        carObject.transform.rotation = carSpawnPoint.transform.rotation;
        carObject.GetComponent<Rigidbody>().velocity = new Vector3(0.0f, 0.0f, 0.0f);
        carObject.GetComponent<Rigidbody>().angularVelocity = new Vector3(0.0f, 0.0f, 0.0f);
        fitnessDeathTimer = 0.0;
        speedDeathTimer = 0.0;
        bestFitnessInThisRun = 0.0;
        distance = 0.0;
        previousPosition = carSpawnPoint.transform.position;
        timer = 0.0;
        nextCheckpoint = 0;
        collisionDetected = false;

        Debug.Log("Generation " + (generationIndex) + " Car: " + generationMemberIndex + " Max: " + totalBestFitness + " Gen: " + (breakthroughGen) + " Car: " + breakthroughCar);

    }

    void NextCar()
    {

        if (generationMemberIndex != -1)
        {
            PostRun();
        }

        generationMemberIndex++;

        if (generationMemberIndex > populationSize - 1)
        {
            NextGeneration();
        }

        PreRun();

    }

}
