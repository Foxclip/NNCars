using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameController : MonoBehaviour
{

    public int populationSize = 10;

    public GameObject carPrefab;
    public GameObject carSpawnPoint;
    public double terminationDelay = 1.0;
    public double terminationSpeed = 0.1;

    [HideInInspector]    
    public List<NeuralNetwork> generation = new List<NeuralNetwork>();

    private GameObject currentCar;
    private double deathTimer = 0.0;

    void Start()
    {

        currentCar = GameObject.Find("Car");

        for (int i = 0; i < populationSize; i++)
        {
            NeuralNetwork newNetwork = new NeuralNetwork(1, 2);
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
    }

    void NextCar()
    {
        currentCar.transform.position = carSpawnPoint.transform.position;
        currentCar.transform.rotation = carSpawnPoint.transform.rotation;
        currentCar.GetComponent<Rigidbody>().velocity = new Vector3(0.0f, 0.0f, 0.0f);
        NeuralNetwork network = new NeuralNetwork(1, 2);
        currentCar.GetComponent<CarController>().neuralNetwork = network;
    }

}
