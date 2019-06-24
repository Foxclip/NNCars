using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameController : MonoBehaviour
{

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
    public bool collisionDetected = false;
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

    private bool fastForward = false;

    void Start()
    {

        currentCar = GameObject.Find("Car");

        for (int i = 0; i < populationSize; i++)
        {
            NeuralNetwork newNetwork = new NeuralNetwork(layerCount, neuronsInLayer);
            newNetwork.Mutate(10, 1);
            generation.Add(newNetwork);
        }
        bestNetwork = generation[0];

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
            fitnessDeathTimer += Time.deltaTime;
        } else
        {
            fitnessDeathTimer = 0.0;
            bestFitnessInThisRun = currentFitness;
            //Debug.Log("SET TO " + bestFitnessInThisRun);
        }

        //Debug.Log(bestFitnessInThisRun);

        if (currentCarVelocity < terminationSpeed)
        {
            speedDeathTimer += Time.deltaTime;
        }
        else
        {
            speedDeathTimer = 0.0;
        }

        double fitness = 0.0;
        if (nextCheckpoint < checkpoints.Length)
        {
            if (Vector3.Distance(currentCar.transform.position, checkpoints[nextCheckpoint].position) < checkpointReachDistance)
            {
                nextCheckpoint++;
            }
            if (nextCheckpoint < checkpoints.Length)
            {
                float distanceToNextCheckpoint = Vector3.Distance(currentCar.transform.position, checkpoints[nextCheckpoint].position);
                fitness = nextCheckpoint * 10.0 + 1.0 / (distanceToNextCheckpoint + 1);
            } else
            {
                fitness = 1000.0;
            }
        }
        else
        {
            fitness = 1000.0;
        }
        if(fitness > totalBestFitness)
        {
            totalBestFitness = fitness;
            breakthroughGen = generationIndex;
        }
        //Debug.Log(fitness);
        generation[generationMemberIndex].fitness = fitness;

    }

    void NextCar()
    {

        //if (generationMemberIndex != -1)
        //{
        //    Debug.Log("Fitness: " + generation[generationMemberIndex].fitness);
        //}

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
                if(i == 0)
                {
                    if (generation[0].fitness > bestNetwork.fitness)
                    {
                        bestNetwork = generation[0];
                        //Debug.Log("NEW BEST Id: " + bestNetwork.id + " Fitness: " + bestNetwork.fitness);
                        //Debug.Log(bestNetwork);
                    }
                    newGeneration.Add(NeuralNetwork.Crossover(bestNetwork, bestNetwork));
                    continue;
                }

                double rand1 = Math.Pow(Utils.Rand(), crossoverPower);
                double rand2 = Math.Pow(Utils.Rand(), crossoverPower);
                double scaledRand1 = rand1 * populationSize;
                double scaledRand2 = rand2 * populationSize;
                int pick1 = (int)scaledRand1;
                int pick2 = (int)scaledRand2;

                NeuralNetwork newNetwork = NeuralNetwork.Crossover(generation[pick1], generation[pick2]);
                newNetwork.Mutate(mutationPower, maxMutation);
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
        nextCheckpoint = 0;
        //carDistance = 0.0;
        collisionDetected = false;

        Debug.Log("Generation " + (generationIndex + 1) + " Car: " + generationMemberIndex + " Max: " + totalBestFitness + " Gen: " + breakthroughGen);

        //Debug.Log("Gen: " + generationIndex + " Car: " + generationMemberIndex + " Id: " +
        //    generation[generationMemberIndex].id + " " +
        //    generation[generationMemberIndex].parent1Id + " " +
        //    generation[generationMemberIndex].parent2Id);

    }

}
