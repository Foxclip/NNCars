﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Allows to configure simulation settings.
/// </summary>
public class StartupSettings : MonoBehaviour
{
    private readonly List<Toggle> inputToggles = new List<Toggle>();        // toggles for choosing available inputs
    private readonly List<Toggle> outputToggles = new List<Toggle>();       // toggles for choosing available outputs
    private readonly List<GameObject> uiControls = new List<GameObject>();  // UI controls for settings

    // prefabs for UI controls
#pragma warning disable IDE0044 // Add readonly modifier
    [SerializeField]
    private GameObject togglePrefab;
    [SerializeField]
    private GameObject settingsTogglePrefab;
    [SerializeField]
    private GameObject intFieldPrefab;
    [SerializeField]
    private GameObject floatFieldPrefab;
    [SerializeField]
    private GameObject dropdownPrefab;
    [SerializeField]
    private GameObject textPrefab;
    [SerializeField]
    private TextMeshProUGUI openFileText;         // text containing filename of neural network
    [SerializeField]
    private TextMeshProUGUI inputOutputCountText; // text informing about changes that will be made in the loaded neural network
#pragma warning restore IDE0044 // Add readonly modifier

    private string networkFile = string.Empty; // current network filename
    private Dropdown selectTrackDropdown;

    /// <summary>
    /// Path to folder neural networks will be saved to.
    /// </summary>
    public static string NetworksFolderPath { get; set; } = "./Networks";

    /// <summary>
    /// Neural network loaded from file.
    /// </summary>
    public static NeuralNetwork SelectedNeuralNetwork { get; set; } = null;

    /// <summary>
    /// Index of selected track.
    /// </summary>
    public static int TrackIndex { get; set; } = 0;

    /// <summary>
    /// Whether saved fitness should be set to 0.
    /// </summary>
    public static bool ResetFitness { get; set; } = false;

    /// <summary>
    /// List of inputs in the simulation.
    /// </summary>
    public static List<string> RegisteredInputs { get; set; } = new List<string>();

    /// <summary>
    /// List of outputs in the simulation.
    /// </summary>
    public static List<string> RegisteredOutputs { get; set; } = new List<string>();

    /// <summary>
    /// Select Network button event.
    /// </summary>
    public void SelectNetworkFile()
    {
        // if there is no "Networks" folder, create it
        Directory.CreateDirectory(NetworksFolderPath);

        string[] fileList = StandaloneFileBrowser.OpenFilePanel("Select network file", NetworksFolderPath, "txt", false);

        if (fileList.Length > 0)
        {
            // getting filename
            this.networkFile = fileList[0];
            this.openFileText.text = Path.GetFileName(this.networkFile);

            this.LoadNetwork(this.networkFile);

            // right track will be selected
            int index = this.selectTrackDropdown.options.FindIndex((x) => x.text == SelectedNeuralNetwork.ExtraProperties["trackName"]);
            if (index != -1)
            {
                this.selectTrackDropdown.value = index;
            }

            // update input/ouput toggles to values from neural network
            foreach (Toggle toggle in this.inputToggles.Concat(this.outputToggles))
            {
                toggle.SetValue(false);
            }
            foreach (Neuron inputNeuron in SelectedNeuralNetwork.InputNeurons)
            {
                Toggle toggle = this.inputToggles.Find((x) => x.GetComponent<TextProperty>().Text == inputNeuron.Name);
                if (toggle != null)
                {
                    toggle.SetValue(true);
                }
            }
            foreach (Neuron outputNeuron in SelectedNeuralNetwork.OutputNeurons)
            {
                Toggle toggle = this.outputToggles.Find((x) => x.GetComponent<TextProperty>().Text == outputNeuron.Name);
                if (toggle != null)
                {
                    toggle.SetValue(true);
                }
            }

            // toggle values were changed, so registered input/output lists have to be updated too
            this.UpdateRegisteredInputsOutputs(RegisteredInputs, this.inputToggles);
            this.UpdateRegisteredInputsOutputs(RegisteredOutputs, this.outputToggles);

            // inputs/outputs in the network might still not match inputs/outputs in the simulation, so neural network has to be updated
            this.UpdateInputsOutputs();
        }
    }

    /// <summary>
    /// Clear Network button event.
    /// </summary>
    public void ClearNetworkFile()
    {
        SelectedNeuralNetwork = null;
        this.networkFile = string.Empty;
        this.openFileText.text = "<none>";
        this.inputOutputCountText.text = string.Empty;
    }

    /// <summary>
    /// Select Track dropdown event.
    /// </summary>
    /// <param name="index">Value of the dropdown.</param>
    public void SelectTrackDropdown(int index)
    {
        TrackIndex = index;
    }

    /// <summary>
    /// Reset Fitness toggle event.
    /// </summary>
    /// <param name="value">Value of the toggle.</param>
    public void ResetFitnessToggle(bool value)
    {
        ResetFitness = value;
    }

    /// <summary>
    /// Start button event.
    /// </summary>
    public void StartSimulation()
    {
        this.FillSettings(GameController.Settings);
        this.FillSettings(CarController.Settings);

        this.SaveConfig();

        SceneManager.LoadScene("MainScene");
    }

    /// <summary>
    /// Fills settings with values from the UI controls.
    /// </summary>
    /// <param name="settings">Settings to be filled.</param>
    private void FillSettings(SettingList settings)
    {
        PropertyInfo[] properties = settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            GameObject control = (from uiControl
                                  in this.uiControls
                                  where uiControl.GetComponent<TextProperty>().Text == property.Name
                                  select uiControl).First();
            switch (property.PropertyType.Name)
            {
                case nameof(Int32):
                    property.SetValue(settings, int.Parse(control.GetComponent<TMP_InputField>().text));
                    break;
                case nameof(Single):
                    property.SetValue(settings, float.Parse(control.GetComponent<TMP_InputField>().text));
                    break;
                case nameof(Boolean):
                    property.SetValue(settings, control.GetComponent<Toggle>().isOn);
                    break;
                default:
                    if (property.PropertyType.IsEnum)
                    {
                        property.SetValue(settings, control.GetComponent<TMP_Dropdown>().value);
                    }
                    else
                    {
                        Debug.Assert(false, string.Format("Setting type {0} is not supproted", property.PropertyType.Name));
                    }
                    break;
            }
        }
    }

    private void Start()
    {
        // things will break if floating point separator is not "."
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

        // turning off debug logging in build
#if UNITY_EDITOR
        Debug.unityLogger.logEnabled = true;
#else
        Debug.unityLogger.logEnabled = false;
#endif

        // there is placeholder text in Editor, but it should be hidden at the start
        this.inputOutputCountText.text = string.Empty;

        // getting some UI controls
        Transform selectTrackTransform = this.transform.Find("Select Track");
        this.selectTrackDropdown = selectTrackTransform.transform.Find("Select Track Dropdown").GetComponent<Dropdown>();

        this.GenerateInputOutputToggles();
        this.GenerateSettingsUIControls();

        this.LoadConfig();

        this.UpdateRegisteredInputsOutputs(RegisteredInputs, this.inputToggles);
        this.UpdateRegisteredInputsOutputs(RegisteredOutputs, this.outputToggles);
    }

    /// <summary>
    /// Updates list of registered inputs or outputs with values from toggles.
    /// </summary>
    /// <param name="registeredList">List of registered inputs or outputs.</param>
    /// <param name="toggleList">List of toggles.</param>
    private void UpdateRegisteredInputsOutputs(List<string> registeredList, List<Toggle> toggleList)
    {
        registeredList.Clear();
        foreach (Toggle toggle in toggleList)
        {
            string name = toggle.GetComponent<TextProperty>().Text;
            if (toggle.isOn)
            {
                registeredList.Add(name);
            }
        }
    }

    /// <summary>
    /// Input toggles event.
    /// </summary>
    private void InputToggleValueChanged()
    {
        this.UpdateRegisteredInputsOutputs(RegisteredInputs, this.inputToggles);

        // because list of inputs changed, neural network has to be updated accordingly
        this.LoadNetwork(this.networkFile);
        this.UpdateInputsOutputs();
    }

    /// <summary>
    /// Output toggles event.
    /// </summary>
    private void OutputToggleValueChanged()
    {
        this.UpdateRegisteredInputsOutputs(RegisteredOutputs, this.outputToggles);

        // because list of outputs changed, neural network has to be updated accordingly
        this.LoadNetwork(this.networkFile);
        this.UpdateInputsOutputs();
    }

    /// <summary>
    /// Generates UI toggles for choosing inputs and outputs available in the simulation.
    /// </summary>
    private void GenerateInputOutputToggles()
    {
        // adding derivatives to the list of inputs
        List<string> inputList = new List<string>(CarController.PossibleInputs);
        foreach (string inputName in CarController.PossibleInputs)
        {
            inputList.Add(inputName + "_D^1");
        }
        foreach (string inputName in CarController.PossibleInputs)
        {
            inputList.Add(inputName + "_D^2");
        }

        // inputs
        for (int i = 0; i < inputList.Count; i++)
        {
            // instantiating toggle prefab
            GameObject newToggle = Instantiate(this.togglePrefab);
            newToggle.transform.SetParent(this.transform.Find("Select Inputs"));

            // related input name will be tied to it
            TextProperty inputName = newToggle.GetComponent<TextProperty>();
            inputName.Text = inputList[i];

            // setting position on canvas
            RectTransform rectTransformComponent = newToggle.GetComponent<RectTransform>();
            rectTransformComponent.anchoredPosition = new Vector2(0.0f, (i * -rectTransformComponent.sizeDelta.y) - 20f);

            // event listener
            Toggle toggleComponent = newToggle.GetComponent<Toggle>();
            toggleComponent.onValueChanged.AddListener(arg0 =>
            {
                this.InputToggleValueChanged();
            });

            // label text
            TextMeshProUGUI labelText = newToggle.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            labelText.text = inputList[i];

            this.inputToggles.Add(toggleComponent);
        }

        // outputs
        for (int i = 0; i < CarController.PossibleOutputs.Count; i++)
        {
            // instantiating toggle prefab
            GameObject newToggle = Instantiate(this.togglePrefab);
            newToggle.transform.SetParent(this.transform.Find("Select Outputs"));

            // related output name will be tied to it
            TextProperty outputName = newToggle.GetComponent<TextProperty>();
            outputName.Text = CarController.PossibleOutputs[i];

            // setting position on canvas
            RectTransform rectTransformComponent = newToggle.GetComponent<RectTransform>();
            rectTransformComponent.anchoredPosition = new Vector2(0.0f, (i * -rectTransformComponent.sizeDelta.y) - 20f);

            // event listener
            Toggle toggleComponent = newToggle.GetComponent<Toggle>();
            toggleComponent.onValueChanged.AddListener(arg0 =>
            {
                this.OutputToggleValueChanged();
            });

            // label text
            TextMeshProUGUI labelText = newToggle.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            labelText.text = CarController.PossibleOutputs[i];

            this.outputToggles.Add(toggleComponent);
        }
    }

    private void GenerateSettingsUIControls()
    {
        this.GenerateSettingsUIControls(GameController.Settings, "SimSettings");
        this.GenerateSettingsUIControls(CarController.Settings, "CarSettings");
    }

    private void GenerateSettingsUIControls(SettingList settings, string parentName)
    {
        PropertyInfo[] properties = settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < properties.Length; i++)
        {
            // creating and positioning label text
            GameObject labelTextObject = Instantiate(this.textPrefab);
            labelTextObject.transform.SetParent(this.transform.Find(parentName));
            RectTransform labelTextRectTransform = labelTextObject.GetComponent<RectTransform>();
            labelTextRectTransform.anchoredPosition = new Vector2(0.0f, (i * -labelTextRectTransform.sizeDelta.y) - 20f);
            TextMeshProUGUI labelText = labelTextObject.GetComponent<TextMeshProUGUI>();
            labelText.text = properties[i].Name;
            labelText.alignment = TextAlignmentOptions.MidlineRight;

            // choosing which UI control to create
            GameObject newUIControl = null;
            if (properties[i].PropertyType == typeof(int))
            {
                newUIControl = Instantiate(this.intFieldPrefab);
                TMP_InputField inputField = newUIControl.GetComponent<TMP_InputField>();
                inputField.text = properties[i].GetValue(settings).ToString();
            }
            else if (properties[i].PropertyType == typeof(float) || properties[i].PropertyType == typeof(double))
            {
                newUIControl = Instantiate(this.floatFieldPrefab);
                TMP_InputField inputField = newUIControl.GetComponent<TMP_InputField>();
                inputField.text = properties[i].GetValue(settings).ToString();
            }
            else if (properties[i].PropertyType == typeof(bool))
            {
                newUIControl = Instantiate(this.settingsTogglePrefab);
                Toggle toggle = newUIControl.GetComponent<Toggle>();
                toggle.isOn = (bool)properties[i].GetValue(settings);
            }
            else if (properties[i].PropertyType.IsEnum)
            {
                newUIControl = Instantiate(this.dropdownPrefab);
                TMP_Dropdown dropdown = newUIControl.GetComponent<TMP_Dropdown>();
                dropdown.AddOptions(System.Enum.GetNames(properties[i].PropertyType).ToList());
                dropdown.value = (int)properties[i].GetValue(settings);
            }
            else
            {
                Debug.Assert(false, string.Format("Unknown property type: {0}", properties[i].PropertyType.Name));
            }

            // remembering which setting this control is supposed to change, will be needed when loading settings from config
            TextProperty settingName = newUIControl.GetComponent<TextProperty>();
            settingName.Text = properties[i].Name;

            newUIControl.transform.SetParent(labelTextObject.transform, false);
            this.uiControls.Add(newUIControl);

            // setting position on canvas
            RectTransform rectTransformComponent = newUIControl.GetComponent<RectTransform>();
            rectTransformComponent.anchoredPosition += new Vector2(170.0f, 0.0f);
        }
    }

    /// <summary>
    /// Makes inputs/outputs of the loaded neural network match inputs/outputs in the simulation.
    /// </summary>
    private void UpdateInputsOutputs()
    {
        if (SelectedNeuralNetwork == null)
        {
            return;
        }

        // counters will be shown to user
        int inputsAdded = 0;
        int inputsRemoved = 0;
        int outputsAdded = 0;
        int outputsRemoved = 0;

        // adding missing inputs
        foreach (string inputName in RegisteredInputs)
        {
            try
            {
                SelectedNeuralNetwork.GetNeuronByName(inputName);
            }
            catch (KeyNotFoundException)
            {
                SelectedNeuralNetwork.AddInputNeuron(inputName, true);
                inputsAdded++;
            }
        }

        // deleting unneeded inputs
        foreach (Neuron inputNeuron in SelectedNeuralNetwork.InputNeurons)
        {
            if (RegisteredInputs.FindAll((x) => x == inputNeuron.Name).Count == 0)
            {
                SelectedNeuralNetwork.RemoveInputNeuron(inputNeuron.Name);
                inputsRemoved++;
            }
        }

        // adding missing outputs
        foreach (string outputName in RegisteredOutputs)
        {
            try
            {
                SelectedNeuralNetwork.GetNeuronByName(outputName);
            }
            catch (KeyNotFoundException)
            {
                SelectedNeuralNetwork.AddOutputNeuron(outputName, true);
                outputsAdded++;
            }
        }

        // deleting unneeded outputs
        foreach (Neuron outputNeuron in SelectedNeuralNetwork.OutputNeurons)
        {
            if (RegisteredOutputs.FindAll((x) => x == outputNeuron.Name).Count == 0)
            {
                SelectedNeuralNetwork.RemoveOutputNeuron(outputNeuron);
                outputsRemoved++;
            }
        }

        // updating UI text
        this.inputOutputCountText.text = string.Format("Inputs: +{0}/-{1}    Outputs: +{2}/-{3}", inputsAdded, inputsRemoved, outputsAdded, outputsRemoved);
    }

    /// <summary>
    /// Loads neural network and car settings from file.
    /// </summary>
    /// <param name="filename">Name of the file.</param>
    private void LoadNetwork(string filename)
    {
        if (this.networkFile == string.Empty)
        {
            return;
        }

        SelectedNeuralNetwork = NeuralNetwork.LoadFromFile(filename);
        PropertyInfo[] properties = CarController.Settings.GetType().GetProperties();
        foreach (PropertyInfo property in properties)
        {
            if (!SelectedNeuralNetwork.ExtraProperties.ContainsKey(property.Name))
            {
                continue;
            }
            string loadedPropertyValue = SelectedNeuralNetwork.ExtraProperties[property.Name];
            switch (property.PropertyType.Name)
            {
                case nameof(Int32):
                    property.SetValue(CarController.Settings, int.Parse(loadedPropertyValue));
                    break;
                case nameof(Single):
                    property.SetValue(CarController.Settings, float.Parse(loadedPropertyValue));
                    break;
                case nameof(Boolean):
                    property.SetValue(CarController.Settings, bool.Parse(loadedPropertyValue));
                    break;
                default:
                    if (property.PropertyType.IsEnum)
                    {
                        property.SetValue(CarController.Settings, Enum.Parse(property.PropertyType, loadedPropertyValue));
                    }
                    else
                    {
                        Debug.Assert(false, $"Setting type {property.PropertyType.Name} is not supproted");
                    }
                    break;
            }
        }
        this.FillControls(CarController.Settings);
    }

    /// <summary>
    /// Loads config file and sets values of UI controls to values from the file.
    /// </summary>
    private void LoadConfig()
    {
        Config config;
        try
        {
            config = Config.Load();
        }
        catch (FileNotFoundException)
        {
            return;
        }
        foreach (SettingList settings in config.Settings)
        {
            this.FillControls(settings);
        }
        this.FillToggles(config.EnabledInputList, this.inputToggles);
        this.FillToggles(config.EnabledOutputList, this.outputToggles);
        this.UpdateRegisteredInputsOutputs(RegisteredInputs, this.inputToggles);
        this.UpdateRegisteredInputsOutputs(RegisteredOutputs, this.outputToggles);
        this.selectTrackDropdown.value = Mathf.Max(0, this.selectTrackDropdown.options.FindIndex((x) => x.text == config.TrackName));
    }

    /// <summary>
    /// Fills toggles with values from the provided list.
    /// Toggle will be enabled if it has TextProperty component with text mathching name from the list.
    /// </summary>
    /// <param name="enabledToggles">List of enabled values.</param>
    /// <param name="toggleList">List of toggles.</param>
    private void FillToggles(List<string> enabledToggles, List<Toggle> toggleList)
    {
        // disabling all toggles
        foreach (Toggle toggle in toggleList)
        {
            toggle.SetValue(false);
        }

        // enabling toggles from the list loaded from config file
        foreach (string enabledToggleName in enabledToggles)
        {
            Toggle toggle = (from toggleControl
                             in toggleList
                             where toggleControl.gameObject.GetComponent<TextProperty>().Text == enabledToggleName
                             select toggleControl).First();
            if (toggle != null)
            {
                toggle.SetValue(true);
            }
        }
    }

    /// <summary>
    /// Fills controls with values loaded from config file.
    /// </summary>
    /// <param name="settings">Settings object.</param>
    private void FillControls(SettingList settings)
    {
        PropertyInfo[] properties = settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            GameObject control = (from uiControl
                                  in this.uiControls
                                  where uiControl.GetComponent<TextProperty>().Text == property.Name
                                  select uiControl).First();
            if (control == null)
            {
                continue;
            }
            switch (property.PropertyType.Name)
            {
                case nameof(Int32):
                case nameof(Single):
                    control.GetComponent<TMP_InputField>().text = property.GetValue(settings).ToString();
                    break;
                case nameof(Boolean):
                    control.GetComponent<Toggle>().isOn = (bool)property.GetValue(settings);
                    break;
                default:
                    if (property.PropertyType.IsEnum)
                    {
                        control.GetComponent<TMP_Dropdown>().value = (int)property.GetValue(settings);
                    }
                    else
                    {
                        Debug.Log(string.Format("Setting type {0} is unknown", property.PropertyType.Name));
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Saves values set by UI controls to config file.
    /// </summary>
    private void SaveConfig()
    {
        string trackName = this.selectTrackDropdown.options[this.selectTrackDropdown.value].text;
        Config config = new Config(new List<SettingList> { GameController.Settings, CarController.Settings }, RegisteredInputs, RegisteredOutputs, trackName);
        config.Save();
    }

    /// <summary>
    /// All settings classes should be derived from this class.
    /// </summary>
    [KnownType(typeof(GameController.SimulationSettings))]
    [KnownType(typeof(CarController.CarSettings))]
    [DataContract(Name = "SettingList")]
    public abstract class SettingList
    {
    }

    /// <summary>
    /// Class for saving/loading settings from config file.
    /// </summary>
    [DataContract(Name = "Config")]
    public class Config
    {
        private const string ConfigPath = "config.xml";

        /// <summary>
        /// Initializes a new instance of the <see cref="Config"/> class.
        /// </summary>
        /// <param name="settings">Settings to be saved.</param>
        /// <param name="enabledInputList">List of enabled inputs to be saved.</param>
        /// <param name="enabledOutputList"> List of enabled outputs to be saved.</param>
        /// <param name="trackName">Name of selected track.</param>
        public Config(List<SettingList> settings, List<string> enabledInputList, List<string> enabledOutputList, string trackName)
        {
            this.Settings = settings;
            this.EnabledInputList = enabledInputList;
            this.EnabledOutputList = enabledOutputList;
            this.TrackName = trackName;
        }

        /// <summary>
        /// Name of selected track.
        /// </summary>
        [DataMember]
        public string TrackName { get; set; }

        /// <summary>
        /// List of enabled inputs.
        /// </summary>
        [DataMember]
        public List<string> EnabledInputList { get; set; }

        /// <summary>
        /// List of enabled outputs.
        /// </summary>
        [DataMember]
        public List<string> EnabledOutputList { get; set; }

        /// <summary>
        /// Settings to be saved.
        /// </summary>
        [DataMember]
        public List<SettingList> Settings { get; set; }

        /// <summary>
        /// Load config from config file.
        /// </summary>
        /// <returns>Config object with lists of settings loaded from file.</returns>
        public static Config Load()
        {
            Config deserializedConfig;
            using (FileStream fs = new FileStream(ConfigPath, FileMode.Open))
            {
                XmlDictionaryReaderQuotas quotas = new XmlDictionaryReaderQuotas
                {
                    MaxDepth = 256,
                };
                XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(fs, quotas);
                DataContractSerializer ser = new DataContractSerializer(typeof(Config));
                deserializedConfig = (Config)ser.ReadObject(reader, true);
                reader.Close();
            }
            return deserializedConfig;
        }

        /// <summary>
        /// Save config to config file.
        /// </summary>
        public void Save()
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
            };
            XmlWriter writer = XmlWriter.Create(ConfigPath, settings);
            DataContractSerializer ser = new DataContractSerializer(typeof(Config));
            ser.WriteObject(writer, this);
            writer.Close();
        }
    }
}
