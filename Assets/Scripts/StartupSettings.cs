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

    public GameObject togglePrefab;                                     //UI toggle prefab

    public static string networksFolderPath = "./Networks";             //path to folder neural networks will be saved to
    public Text openFileText;                                           //text containing filename of neural network
    public Text inputOutputCountText;                                   //text informing about changes that will be made in the loaded neural network

    public static NeuralNetwork neuralNetwork = null;                   //neural network loaded from file
    public static int trackIndex = 0;
    public static bool resetFitness = false;                            //whether fitness of loaded neural network will be reset

    public static List<string> registeredInputs = new List<string>();   //list of inputs in the simulation
    public static List<string> registeredOutputs = new List<string>();  //list of outputs in the simulation

    private List<Toggle> inputToggles = new List<Toggle>();             //toggles for choosing available inputs
    private List<Toggle> outputToggles = new List<Toggle>();            //toggles for choosing available outputs
    private string networkFile = "";                                    //current network filename

    void Start()
    {

        //there is placeholder text in Editor, but it should be hidden at the start
        inputOutputCountText.text = "";

        GenerateToggles();
        UpdateRegisteredInputs();
        UpdateRegisteredOutputs();

    }

    void UpdateRegisteredInputs()
    {
        registeredInputs.Clear();
        foreach(Toggle toggle in inputToggles)
        {
            string inputName = toggle.GetComponent<TextProperty>().text;
            if (toggle.isOn)
            {
                registeredInputs.Add(inputName);
            }
        }
    }

    void UpdateRegisteredOutputs()
    {
        registeredOutputs.Clear();
        foreach (Toggle toggle in outputToggles)
        {
            string outputName = toggle.GetComponent<TextProperty>().text;
            if (toggle.isOn)
            {
                registeredOutputs.Add(outputName);
            }
        }
    }

    void InputToggleValueChanged(Toggle toggle, bool value, string inputName)
    {
        Debug.Log("CHANGED " + toggle.GetInstanceID() + " " + inputName + " " + value);
        UpdateRegisteredInputs();
        LoadNetwork(networkFile);
    }

    void OutputToggleValueChanged(Toggle toggle, bool value, string outputName)
    {
        Debug.Log("CHANGED " + toggle.GetInstanceID() + " " + outputName + " " + value);
        UpdateRegisteredOutputs();
        LoadNetwork(networkFile);
    }

    /// <summary>
    /// Generates UI toggles for choosing inputs and outputs available in the simulation.
    /// </summary>
    void GenerateToggles()
    {

        //inputs
        for(int i = 0; i < CarController.possibleInputs.Count; i++)
        {

            //instantiating toggle prefab
            GameObject newToggle = Instantiate(togglePrefab);
            newToggle.transform.SetParent(transform.Find("Select Inputs"));

            //related input name will be tied to it
            TextProperty inputName = newToggle.GetComponent<TextProperty>();
            inputName.text = CarController.possibleInputs[i];

            //setting position on canvas
            RectTransform rectTransformComponent = newToggle.GetComponent<RectTransform>();
            rectTransformComponent.anchoredPosition = new Vector2(0.0f, i * -rectTransformComponent.sizeDelta.y - 20f);

            //event listener
            Toggle toggleComponent = newToggle.GetComponent<Toggle>();
            toggleComponent.onValueChanged.AddListener(delegate {
                InputToggleValueChanged(toggleComponent, toggleComponent.isOn, toggleComponent.gameObject.GetComponent<TextProperty>().text);
            });

            //label text
            Text labelText = newToggle.transform.Find("Label").GetComponent<Text>();
            labelText.text = CarController.possibleInputs[i];

            inputToggles.Add(toggleComponent);

        }

        //outputs
        for (int i = 0; i < CarController.possibleOutputs.Count; i++)
        {

            //instantiating toggle prefab
            GameObject newToggle = Instantiate(togglePrefab);
            newToggle.transform.SetParent(transform.Find("Select Outputs"));

            //related output name will be tied to it
            TextProperty outputName = newToggle.GetComponent<TextProperty>();
            outputName.text = CarController.possibleOutputs[i];

            //setting position on canvas
            RectTransform rectTransformComponent = newToggle.GetComponent<RectTransform>();
            rectTransformComponent.anchoredPosition = new Vector2(0.0f, i * -rectTransformComponent.sizeDelta.y - 20f);

            //event listener
            Toggle toggleComponent = newToggle.GetComponent<Toggle>();
            toggleComponent.onValueChanged.AddListener(delegate {
                OutputToggleValueChanged(toggleComponent, toggleComponent.isOn, toggleComponent.gameObject.GetComponent<TextProperty>().text);
            });

            //label text
            Text labelText = newToggle.transform.Find("Label").GetComponent<Text>();
            labelText.text = CarController.possibleOutputs[i];

            outputToggles.Add(toggleComponent);

        }

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
        foreach (string inputName in registeredInputs)
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
            if (registeredInputs.FindAll((x) => x == inputNeuron.name).Count == 0)
            {
                neuralNetwork.RemoveInputNeuron(inputNeuron.name);
                inputsRemoved++;
            }
        }

        //adding missing outputs
        foreach (string outputName in registeredOutputs)
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
            if (registeredOutputs.FindAll((x) => x == outputNeuron.name).Count == 0)
            {
                neuralNetwork.RemoveOutputNeuron(outputNeuron);
                outputsRemoved++;
            }
        }

        //updating UI text
        inputOutputCountText.text = String.Format("Inputs: +{0}/-{1}    Outputs: +{2}/-{3}", inputsAdded, inputsRemoved, outputsAdded, outputsRemoved);

    }

    public void LoadNetwork(string filename)
    {

        if(networkFile == "")
        {
            return;
        }

        neuralNetwork = NeuralNetwork.LoadFromFile(filename);

        //inputs/outputs in the network might not match inputs/outputs in the simulation, so neural network has to be updated
        UpdateInputsOutputs();

    }

    public void SelectNetworkFile()
    {

        //if there is no "Networks" folder, create it
        Directory.CreateDirectory(networksFolderPath);

        string[] fileList = StandaloneFileBrowser.OpenFilePanel("Select network file", networksFolderPath, "txt", false);

        if(fileList.Length > 0)
        {

            //getting filename
            networkFile = fileList[0];
            openFileText.text = Path.GetFileName(networkFile);

            LoadNetwork(networkFile);

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
