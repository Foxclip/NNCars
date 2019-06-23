using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameController : MonoBehaviour
{

    public int populationSize = 10;
    public float crossoverPower = 2;
    public float mutationPower = 10;
    public float maxMutation = 1;
    public float speedupTimeScale = 10;

    public GameObject carPrefab;
    public GameObject carSpawnPoint;
    public GameObject finish;
    public double terminationDelay = 1.0;
    public double terminationSpeed = 0.1;

    [HideInInspector]    
    public List<NeuralNetwork> generation = new List<NeuralNetwork>();

    private GameObject currentCar;
    private NeuralNetwork bestNetwork;
    private double deathTimer = 0.0;
    private int generationIndex = 0;
    private int generationMemberIndex = -1;
    private Vector3 previousPosition;
    private double carDistance = 0.0;

    private bool fastForward = false;

    void Start()
    {

        currentCar = GameObject.Find("Car");

        for (int i = 0; i < populationSize; i++)
        {
            NeuralNetwork newNetwork = new NeuralNetwork(1, 2);
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

        if(deathTimer > terminationDelay || currentCar.transform.position.y < 0.0f)
        {
            NextCar();
        }
        if(currentCarVelocity < terminationSpeed)
        {
            deathTimer += Time.deltaTime;
        } else
        {
            deathTimer = 0.0;
        }

        carDistance += Vector3.Distance(currentCar.transform.position, previousPosition);
        previousPosition = currentCar.transform.position;
        generation[generationMemberIndex].fitness = carDistance;

    }

    void NextCar()
    {

        if (generationMemberIndex != -1)
        {
            Debug.Log("Fitness: " + generation[generationMemberIndex].fitness);
        }

        if (generationMemberIndex == 0) {
            Debug.Log("generation[0]");
            Debug.Log(generation[0]);
        }

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
                        Debug.Log("NEW BEST Id: " + bestNetwork.id + " Fitness: " + bestNetwork.fitness);
                        Debug.Log(bestNetwork);
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

        deathTimer = 0.0;
        previousPosition = currentCar.transform.position;
        carDistance = 0.0;

        Debug.Log("Gen: " + generationIndex + " Car: " + generationMemberIndex + " Id: " +
            generation[generationMemberIndex].id + " " +
            generation[generationMemberIndex].parent1Id + " " +
            generation[generationMemberIndex].parent2Id);

    }

}
