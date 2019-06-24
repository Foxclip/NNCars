using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameController : MonoBehaviour
{

    private const int INPUT_COUNT = 5;

    public int layerCount = 1;
    public int neuronsInLayer = 16;
    public int populationSize = 10;
    public float crossoverPower = 2;
    public float mutationPower = 10;
    public float maxMutation = 1;
    public float speedupTimeScale = 10;
    public float checkpointReachDistance = 3.0f;

    public GameObject carPrefab;
    public GameObject carSpawnPoint;
    public Transform[] checkpoints;
    public double terminationDelay = 1.0;
    public double terminationSpeed = 0.2;

    [HideInInspector]
    public List<NeuralNetwork> generation = new List<NeuralNetwork>();
    [HideInInspector]
    public bool collisionDetected = false;
    [HideInInspector]
    public int nextCheckpoint = 0;

    private GameObject currentCar;
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
    private Vector3 previousPosition;

    private bool fastForward = false;

    void Start()
    {

        currentCar = GameObject.Find("Car");

        for (int i = 0; i < populationSize; i++)
        {
            NeuralNetwork newNetwork = new NeuralNetwork(INPUT_COUNT, layerCount, neuronsInLayer);
            newNetwork.Mutate(1, maxMutation * Math.Pow((double)i / populationSize, mutationPower));
            generation.Add(newNetwork);
        }
        bestNetwork = generation[0];

        previousPosition = carSpawnPoint.transform.position;

        NextCar();

    }

    void Update()
    {
        if(Input.GetKeyDown("space"))
        {
            if(fastForward)
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

        double currentCarVelocity = currentCar.GetComponent<Rigidbody>().velocity.magnitude;
        double currentFitness = generation[generationMemberIndex].fitness;
        //Debug.Log(distance);

        distance += Vector3.Distance(currentCar.transform.position, previousPosition);
        previousPosition = currentCar.transform.position;
        timer += Time.fixedDeltaTime;

        if (fitnessDeathTimer > terminationDelay || speedDeathTimer > terminationDelay || currentCar.transform.position.y < 0.0f || collisionDetected)
        {
            //if(fitnessDeathTimer > terminationDelay)
            //{
            //    Debug.Log("FITNESS Max: " + bestFitnessInThisRun + " Current: " + currentFitness);
            //}
            //if (speedDeathTimer > terminationDelay)
            //{
            //    Debug.Log("SPEED");
            //}
            NextCar();
            return;
        }

        if(currentFitness <= bestFitnessInThisRun)
        {
            fitnessDeathTimer += Time.fixedDeltaTime;
        } else
        {
            fitnessDeathTimer = 0.0;
            bestFitnessInThisRun = currentFitness;
            //Debug.Log("SET TO " + bestFitnessInThisRun);
        }

        //Debug.Log(bestFitnessInThisRun);

        if (currentCarVelocity < terminationSpeed)
        {
            speedDeathTimer += Time.fixedDeltaTime;
        }
        else
        {
            speedDeathTimer = 0.0;
        }

        double checkpointBonus = 0.0;
        double distanceBonus = 0.0;
        if (nextCheckpoint < checkpoints.Length)
        {
            if (Vector3.Distance(currentCar.transform.position, checkpoints[nextCheckpoint].position) < checkpointReachDistance)
            {
                nextCheckpoint++;
            }
        }
        if (nextCheckpoint < checkpoints.Length)
        {
            float distanceToNextCheckpoint = Vector3.Distance(currentCar.transform.position, checkpoints[nextCheckpoint].position);
            distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * 10.0;
            checkpointBonus = nextCheckpoint * 100.0;
        }
        else
        {
            checkpointBonus = 1000.0;
        }
        //Debug.Log(fitness);
        double fitness = checkpointBonus + distanceBonus;
        generation[generationMemberIndex].fitness = fitness;

    }

    void NextCar()
    {

        if (generationMemberIndex != -1)
        {
            double averageSpeed = distance / timer;
            double distanceBonus = 0.0;
            if (nextCheckpoint < checkpoints.Length)
            {
                float distanceToNextCheckpoint = Vector3.Distance(currentCar.transform.position, checkpoints[nextCheckpoint].position);
                distanceBonus = 1.0 / (distanceToNextCheckpoint + 1) * 10.0;
            }
            double checkpointBonus = nextCheckpoint * 100.0;
            double speedBonus = Math.Tanh(averageSpeed / 100.0);
            double savedFitness = generation[generationMemberIndex].fitness;
            generation[generationMemberIndex].fitness += speedBonus;
            double fitness = generation[generationMemberIndex].fitness;
            if (fitness > totalBestFitness)
            {
                totalBestFitness = fitness;
                breakthroughGen = generationIndex;
                breakthroughCar = generationMemberIndex;
            }

            Debug.Log("Fitness: " + generation[generationMemberIndex].fitness + " Nsb: " + savedFitness + " Time: " + timer + " Distance: " + distance + " Avg sp: " + averageSpeed);
            Debug.Log("Chk: " + checkpointBonus + " Dst: " + distanceBonus + " Spd: " + speedBonus);
        }

        //if (generationMemberIndex == 0) {
        //    Debug.Log("generation[0]");
        //    Debug.Log(generation[0]);
        //}

        generationMemberIndex++;

        if (generationMemberIndex > populationSize - 1)
        {

            generation.Sort((x, y) => -x.fitness.CompareTo(y.fitness));

            List<NeuralNetwork> newGeneration = new List<NeuralNetwork>();
            for(int i = 0; i < populationSize; i++)
            {


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

        NeuralNetwork network = generation[generationMemberIndex];
        currentCar.GetComponent<CarController>().neuralNetwork = network;

        currentCar.transform.position = carSpawnPoint.transform.position;
        currentCar.transform.rotation = carSpawnPoint.transform.rotation;
        currentCar.GetComponent<Rigidbody>().velocity = new Vector3(0.0f, 0.0f, 0.0f);
        currentCar.GetComponent<Rigidbody>().angularVelocity = new Vector3(0.0f, 0.0f, 0.0f);
        fitnessDeathTimer = 0.0;
        speedDeathTimer = 0.0;
        bestFitnessInThisRun = 0.0;
        //Debug.Log("BEST " + bestFitnessInThisRun);
        distance = 0.0;
        previousPosition = carSpawnPoint.transform.position;
        timer = 0.0;
        nextCheckpoint = 0;
        //carDistance = 0.0;
        collisionDetected = false;
        currentCar.GetComponent<Rigidbody>().isKinematic = true;

        Debug.Log("Generation " + (generationIndex + 1) + " Car: " + generationMemberIndex + " Max: " + totalBestFitness + " Gen: " + (breakthroughGen + 1) + " Car: " + breakthroughCar);


        //Debug.Log("Gen: " + generationIndex + " Car: " + generationMemberIndex + " Id: " +
        //    generation[generationMemberIndex].id + " " +
        //    generation[generationMemberIndex].parent1Id + " " +
        //    generation[generationMemberIndex].parent2Id);

    }

}
