using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using SFB;

[DataContract(Name = "Setting")]
[KnownType(typeof(IntSetting))]
[KnownType(typeof(FloatSetting))]
[KnownType(typeof(BoolSetting))]
[KnownType(typeof(ChoiceSetting))]
public class Setting
{

    [DataMember]
    public string name;

    public GameObject control;

    public Setting(string name)
    {
        this.name = name;
    }

}

[DataContract(Name = "IntSetting")]
public class IntSetting : Setting
{

    [DataMember]
    public int value;

    public IntSetting(string name, int value) : base(name)
    {
        this.name = name;
        this.value = value;
    }

}

[DataContract(Name = "FloatSetting")]
public class FloatSetting : Setting
{

    [DataMember]
    public float value;

    public FloatSetting(string name, float value) : base(name)
    {
        this.name = name;
        this.value = value;
    }

}

[DataContract(Name = "BoolSetting")]
public class BoolSetting : Setting
{

    [DataMember]
    public bool value;

    public BoolSetting(string name, bool value) : base(name)
    {
        this.name = name;
        this.value = value;
    }

}

[DataContract(Name = "ChoiceSetting")]
public class ChoiceSetting : Setting
{

    [DataMember]
    public int value;

    public List<string> choices;


    public ChoiceSetting(string name, List<string> choices, int value) : base(name)
    {
        this.name = name;
        this.choices = choices;
        this.value = value;
    }

}

[DataContract(Name = "Config")]
public class Config
{
    const string configPath = "config.xml";

    [DataMember]
    public List<Setting> settings = new List<Setting>();
    [DataMember]
    public List<string> enabledInputList = new List<string>();
    [DataMember]
    public List<string> enabledOutputList = new List<string>();

    public Config(List<Setting> settings)
    {
        this.settings = settings;
    }

    public static Config Load()
    {
        Config deserializedConfig;
        using (FileStream fs = new FileStream(configPath, FileMode.Open))
        {
            XmlDictionaryReaderQuotas quotas = new XmlDictionaryReaderQuotas
            {
                MaxDepth = 256
            };
            XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(fs, quotas);
            DataContractSerializer ser = new DataContractSerializer(typeof(Config));
            deserializedConfig = (Config)ser.ReadObject(reader, true);
            reader.Close();
        }
        return deserializedConfig;
    }

    public void Save()
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "    "
        };
        XmlWriter writer = XmlWriter.Create(configPath, settings);
        DataContractSerializer ser = new DataContractSerializer(typeof(Config));
        ser.WriteObject(writer, this);
        writer.Close();
    }

}

public class StartupSettings : MonoBehaviour
{

    //prefabs for UI controls
    public GameObject togglePrefab;
    public GameObject settingsTogglePrefab;
    public GameObject intFieldPrefab;
    public GameObject floatFieldPrefab;
    public GameObject dropdownPrefab;
    public GameObject textPrefab;

    public static string networksFolderPath = "./Networks";             //path to folder neural networks will be saved to
    public TextMeshProUGUI openFileText;                                //text containing filename of neural network
    public TextMeshProUGUI inputOutputCountText;                        //text informing about changes that will be made in the loaded neural network

    public static NeuralNetwork neuralNetwork = null;                   //neural network loaded from file
    public static int trackIndex = 0;
    public static bool resetFitness = false;                            //whether fitness of loaded neural network will be reset

    public static List<string> registeredInputs = new List<string>();   //list of inputs in the simulation
    public static List<string> registeredOutputs = new List<string>();  //list of outputs in the simulation

    private string networkFile = "";                                    //current network filename

    private readonly List<Toggle> inputToggles = new List<Toggle>();             //toggles for choosing available inputs
    private readonly List<Toggle> outputToggles = new List<Toggle>();            //toggles for choosing available outputs

    private static readonly List<Setting> settingsList = new List<Setting>();

#pragma warning disable IDE0051 // Remove unused private members
    void Start()
#pragma warning restore IDE0051 // Remove unused private members
    {

        //things will break if floating point separator is not "."
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

        //there is placeholder text in Editor, but it should be hidden at the start
        inputOutputCountText.text = "";

        GenerateInputOutputToggles();
        UpdateRegisteredInputs();
        UpdateRegisteredOutputs();

        GenerateSettingsUIControls();

        LoadConfig();

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
    void GenerateInputOutputToggles()
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
            TextMeshProUGUI labelText = newToggle.transform.Find("Label").GetComponent<TextMeshProUGUI>();
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
            TextMeshProUGUI labelText = newToggle.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            labelText.text = CarController.possibleOutputs[i];

            outputToggles.Add(toggleComponent);

        }

    }

    public static int GetIntSetting(string name)
    {
        return ((IntSetting)GetSetting(name)).value;
    }

    public static float GetFloatSetting(string name)
    {
        return ((FloatSetting)GetSetting(name)).value;
    }

    public static bool GetBoolSetting(string name)
    {
        return ((BoolSetting)GetSetting(name)).value;
    }

    public static int GetChoiceSetting(string name)
    {
        return ((ChoiceSetting)GetSetting(name)).value;
    }

    private static Setting GetSetting(string name)
    {
        List<Setting> matchingSettings = (from setting in settingsList where setting.name == name select setting).ToList();
        if(matchingSettings.Count == 0)
        {
            throw new KeyNotFoundException(String.Format("Setting {0} not found", name));
        }
        return matchingSettings[0];
    }

    void GenerateSettingsUIControls()
    {
        GenerateSettingsUIControls(GameController.Settings, "SimSettings");
        GenerateSettingsUIControls(CarController.settings, "CarSettings");
    }

    void GenerateSettingsUIControls(List<Setting> settings, string parentName)
    {

        for (int i = 0; i < settings.Count; i++)
        {

            //creating and positioning label text
            GameObject labelTextObject = Instantiate(textPrefab);
            labelTextObject.transform.SetParent(transform.Find(parentName));
            RectTransform labelTextRectTransform = labelTextObject.GetComponent<RectTransform>();
            labelTextRectTransform.anchoredPosition = new Vector2(0.0f, i * -labelTextRectTransform.sizeDelta.y - 20f);
            TextMeshProUGUI labelText = labelTextObject.GetComponent<TextMeshProUGUI>();
            labelText.text = settings[i].name;
            labelText.alignment = TextAlignmentOptions.MidlineRight;

            //choosing which UI control to create
            GameObject newUIControl = null;
            if(settings[i].GetType() == typeof(IntSetting))
            {
                newUIControl = Instantiate(intFieldPrefab);
                TMP_InputField inputField = newUIControl.GetComponent<TMP_InputField>();
                inputField.text = ((IntSetting)settings[i]).value.ToString();
            }
            if (settings[i].GetType() == typeof(FloatSetting))
            {
                newUIControl = Instantiate(floatFieldPrefab);
                TMP_InputField inputField = newUIControl.GetComponent<TMP_InputField>();
                inputField.text = ((FloatSetting)settings[i]).value.ToString();
            }
            if(settings[i].GetType() == typeof(BoolSetting))
            {
                newUIControl = Instantiate(settingsTogglePrefab);
                Toggle toggle = newUIControl.GetComponent<Toggle>();
                toggle.isOn = ((BoolSetting)settings[i]).value;
            }
            if (settings[i].GetType() == typeof(ChoiceSetting))
            {
                newUIControl = Instantiate(dropdownPrefab);
                TMP_Dropdown dropdown = newUIControl.GetComponent<TMP_Dropdown>();
                ChoiceSetting setting = ((ChoiceSetting)settings[i]);
                dropdown.AddOptions(setting.choices);
                dropdown.value = setting.value;
            }

            settings[i].control = newUIControl;
            settingsList.Add(settings[i]);
            newUIControl.transform.SetParent(labelTextObject.transform, false);

            //related input name will be tied to it
            TextProperty settingName = newUIControl.GetComponent<TextProperty>();
            settingName.text = settings[i].name;

            //setting position on canvas
            RectTransform rectTransformComponent = newUIControl.GetComponent<RectTransform>();
            rectTransformComponent.anchoredPosition += new Vector2(170.0f, 0.0f);


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

    /// <summary>
    /// Loads config file and sets values of UI controls to values from the file
    /// </summary>
    void LoadConfig()
    {
        Config config;
        try
        {
            config = Config.Load();
        }
        catch(FileNotFoundException)
        {
            return;
        }
        foreach(Setting settingFromConfig in config.settings)
        {
            GameObject control = (from settingFromSettingsList 
                                  in settingsList
                                  where settingFromSettingsList.name == settingFromConfig.name
                                  select settingFromSettingsList.control).First();
            if(control == null)
            {
                continue;
            }
            switch (settingFromConfig.GetType().Name)
            {
                case nameof(IntSetting):    control.GetComponent<TMP_InputField>().text = ((IntSetting)settingFromConfig).value.ToString();     break;
                case nameof(FloatSetting):  control.GetComponent<TMP_InputField>().text = ((FloatSetting)settingFromConfig).value.ToString();   break;
                case nameof(BoolSetting):   control.GetComponent<Toggle>().isOn         = ((BoolSetting)settingFromConfig).value;               break;
                case nameof(ChoiceSetting): control.GetComponent<TMP_Dropdown>().value  = ((ChoiceSetting)settingFromConfig).value;             break;
                default: Debug.Log(String.Format("Setting type {0} is unknown", settingFromConfig.GetType().Name)); break;
            }
        }
    }

    /// <summary>
    /// Saves values set by UI controls to config file
    /// </summary>
    void SaveConfig()
    {
        Config config = new Config(settingsList);
        config.Save();
    }

    public void StartSimulation()
    {

        //GameObjects are going to be destroyed, so data has to be saved
        foreach(Setting setting in settingsList)
        {
            GameObject control = setting.control;
            switch(setting.GetType().Name)
            {
                case nameof(IntSetting):    ((IntSetting)setting).value    = Int32.Parse(control.GetComponent<TMP_InputField>().text); break;
                case nameof(FloatSetting):  ((FloatSetting)setting).value  = float.Parse(control.GetComponent<TMP_InputField>().text); break;
                case nameof(BoolSetting):   ((BoolSetting)setting).value   = control.GetComponent<Toggle>().isOn;                      break;
                case nameof(ChoiceSetting): ((ChoiceSetting)setting).value = control.GetComponent<TMP_Dropdown>().value;               break;
            }
        }

        SaveConfig();

        SceneManager.LoadScene("MainScene");

    }

}
