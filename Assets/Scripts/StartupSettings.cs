using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SFB;

public class StartupSettings : MonoBehaviour
{

    public static string networksFolderPath = "./Networks";     //path to folder neural networks will be saved to
    public Text openFileText;                                   //text containing filename of neural network
    public Text inputOutputCountText;                           //text informing about changes that will be made in the loaded neural network

    public static NeuralNetwork neuralNetwork = null;           //neural network loaded from file
    public static int trackIndex = 0;
    public static bool resetFitness = false;                    //whether fitness of loaded neural network will be reset

    void Start()
    {

        //there is placeholder text in Editor, but it should be hidden at the start
        inputOutputCountText.text = "";

    }

    void Update()
    {
        
    }

    /// <summary>
    /// Makes inputs/outputs of the loaded neural network match inputs/outputs in the simulation
    /// </summary>
    void UpdateInputsOutputs()
    {

        //counters will be shown to user
        int inputsAdded = 0;
        int inputsRemoved = 0;
        int outputsAdded = 0;
        int outputsRemoved = 0;

        //adding missing inputs
        foreach (string inputName in CarController.registeredInputs)
        {
            try
            {
                neuralNetwork.GetNeuronByName(inputName);
            }
            catch (KeyNotFoundException)
            {
                neuralNetwork.AddInputNeuron(inputName, true);
                inputsAdded++;
            }
        }

        //deleting unneeded inputs
        foreach (Neuron inputNeuron in neuralNetwork.GetInputNeurons())
        {
            if (CarController.registeredInputs.FindAll((x) => x == inputNeuron.name).Count == 0)
            {
                neuralNetwork.RemoveInputNeuron(inputNeuron.name);
                inputsRemoved++;
            }
        }

        //adding missing outputs
        foreach (string outputName in CarController.registeredOutputs)
        {
            try
            {
                neuralNetwork.GetNeuronByName(outputName);
            }
            catch (KeyNotFoundException)
            {
                neuralNetwork.AddOutputNeuron(outputName, true);
                outputsAdded++;
            }
        }

        //deleting unneeded outputs
        foreach (Neuron outputNeuron in neuralNetwork.GetOutputNeurons())
        {
            if (CarController.registeredOutputs.FindAll((x) => x == outputNeuron.name).Count == 0)
            {
                neuralNetwork.RemoveOutputNeuron(outputNeuron);
                outputsRemoved++;
            }
        }

        //updating UI text
        inputOutputCountText.text = String.Format("Inputs: +{0}/-{1}    Outputs: +{2}/-{3}", inputsAdded, inputsRemoved, outputsAdded, outputsRemoved);

    }

    public void SelectNetworkFile()
    {

        //if there is no "Networks" folder, create it
        Directory.CreateDirectory(networksFolderPath);

        //open file dialog
        string[] fileList = StandaloneFileBrowser.OpenFilePanel("Select network file", networksFolderPath, "txt", false);
        if(fileList.Length > 0)
        {

            //getting filename
            string networkFile = fileList[0];
            openFileText.text = Path.GetFileName(networkFile);

            //loading network
            neuralNetwork = NeuralNetwork.LoadFromFile(networkFile);

            //inputs/outputs in the network might not match inputs/outputs in the simulation, so neural network has to be updated
            UpdateInputsOutputs();

        }

    }

    public void ClearNetworkFile()
    {
        neuralNetwork = null;
        openFileText.text = "<none>";
        inputOutputCountText.text = "";
    }

    public void ResetFitnessToggle(bool value)
    {
        resetFitness = value;
    }

    public void SelectTrackDropdown(int index)
    {
        trackIndex = index;
    }

    public void StartSimulation()
    {
        SceneManager.LoadScene("MainScene");
    }

}
