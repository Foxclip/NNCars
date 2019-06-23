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

    public GameObject carPrefab;
    public GameObject carSpawnPoint;
    public GameObject finish;
    public double terminationDelay = 1.0;
    public double terminationSpeed = 0.1;

    [HideInInspector]    
    public List<NeuralNetwork> generation = new List<NeuralNetwork>();

    private GameObject currentCar;
    private double deathTimer = 0.0;
    private int generationIndex = 0;
    private int generationMemberIndex = -1;

    void Start()
    {

        currentCar = GameObject.Find("Car");

        for (int i = 0; i < populationSize; i++)
        {
            NeuralNetwork newNetwork = new NeuralNetwork(1, 2);
            newNetwork.Mutate(10, 1);
            generation.Add(newNetwork);
        }

        NextCar();

    }

    void Update()
    {
        
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

        generation[generationMemberIndex].fitness = 1.0 / (Vector3.Distance(currentCar.transform.position, finish.transform.position) + 1.0);

    }

    void NextCar()
    {

        if (generationMemberIndex != -1)
        {
            Debug.Log("Fitness: " + generation[generationMemberIndex].fitness);
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
                    NeuralNetwork newBestNetwork = NeuralNetwork.Crossover(generation[0], generation[0]);
                    newGeneration.Add(newBestNetwork);
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

        Debug.Log("Gen: " + generationIndex + " Car: " + generationMemberIndex + "Id: " + 
            generation[generationMemberIndex].id + " " + 
            generation[generationMemberIndex].parent1Id + " " + 
            generation[generationMemberIndex].parent2Id);

    }

}
