using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private static readonly List<Setting> SettingsList = new List<Setting>();
    private readonly List<Toggle> inputToggles = new List<Toggle>();  // toggles for choosing available inputs
    private readonly List<Toggle> outputToggles = new List<Toggle>(); // toggles for choosing available outputs

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
    /// Whether fitness of loaded neural network will be reset.
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
    /// Retuns value of IntSetting with specified name.
    /// </summary>
    /// <param name="name">Name of the setting.</param>
    /// <returns>Value of the setting.</returns>
    public static int GetIntSetting(string name)
    {
        return ((IntSetting)GetSetting(name)).Value;
    }

    /// <summary>
    /// Retuns value of FloatSetting with specified name.
    /// </summary>
    /// <param name="name">Name of the setting.</param>
    /// <returns>Value of the setting.</returns>
    public static float GetFloatSetting(string name)
    {
        return ((FloatSetting)GetSetting(name)).Value;
    }

    /// <summary>
    /// Retuns value of BoolSetting with specified name.
    /// </summary>
    /// <param name="name">Name of the setting.</param>
    /// <returns>Value of the setting.</returns>
    public static bool GetBoolSetting(string name)
    {
        return ((BoolSetting)GetSetting(name)).Value;
    }

    /// <summary>
    /// Retuns value of ChoiceSetting with specified name.
    /// </summary>
    /// <param name="name">Name of the setting.</param>
    /// <returns>Value of the setting.</returns>
    public static int GetChoiceSetting(string name)
    {
        return ((ChoiceSetting)GetSetting(name)).Value;
    }

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
        }
    }

    /// <summary>
    /// Clear Network button event.
    /// </summary>
    public void ClearNetworkFile()
    {
        SelectedNeuralNetwork = null;
        this.openFileText.text = "<none>";
        this.inputOutputCountText.text = string.Empty;
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
    /// Select Track dropdown event.
    /// </summary>
    /// <param name="index">Value of the dropdown.</param>
    public void SelectTrackDropdown(int index)
    {
        TrackIndex = index;
    }

    /// <summary>
    /// Start button event.
    /// </summary>
    public void StartSimulation()
    {
        // GameObjects are going to be destroyed, so data has to be saved
        foreach (Setting setting in SettingsList)
        {
            GameObject control = setting.Control;
            switch (setting.GetType().Name)
            {
                case nameof(IntSetting): ((IntSetting)setting).Value = int.Parse(control.GetComponent<TMP_InputField>().text); break;
                case nameof(FloatSetting): ((FloatSetting)setting).Value = float.Parse(control.GetComponent<TMP_InputField>().text); break;
                case nameof(BoolSetting): ((BoolSetting)setting).Value = control.GetComponent<Toggle>().isOn; break;
                case nameof(ChoiceSetting): ((ChoiceSetting)setting).Value = control.GetComponent<TMP_Dropdown>().value; break;
            }
        }

        this.SaveConfig();

        SceneManager.LoadScene("MainScene");
    }

    private static Setting GetSetting(string name)
    {
        List<Setting> matchingSettings = (from setting in SettingsList where setting.Name == name select setting).ToList();
        if (matchingSettings.Count == 0)
        {
            throw new KeyNotFoundException(string.Format("Setting {0} not found", name));
        }
        return matchingSettings[0];
    }

    private void Start()
    {
        // things will break if floating point separator is not "."
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

        // there is placeholder text in Editor, but it should be hidden at the start
        this.inputOutputCountText.text = string.Empty;

        this.GenerateInputOutputToggles();
        this.UpdateRegisteredInputs();
        this.UpdateRegisteredOutputs();

        this.GenerateSettingsUIControls();

        this.LoadConfig();
    }

    private void UpdateRegisteredInputs()
    {
        RegisteredInputs.Clear();
        foreach (Toggle toggle in this.inputToggles)
        {
            string inputName = toggle.GetComponent<TextProperty>().Text;
            if (toggle.isOn)
            {
                RegisteredInputs.Add(inputName);
            }
        }
    }

    private void UpdateRegisteredOutputs()
    {
        RegisteredOutputs.Clear();
        foreach (Toggle toggle in this.outputToggles)
        {
            string outputName = toggle.GetComponent<TextProperty>().Text;
            if (toggle.isOn)
            {
                RegisteredOutputs.Add(outputName);
            }
        }
    }

    private void InputToggleValueChanged(Toggle toggle, bool value, string inputName)
    {
        Debug.Log("CHANGED " + toggle.GetInstanceID() + " " + inputName + " " + value);
        this.UpdateRegisteredInputs();
        this.LoadNetwork(this.networkFile);
    }

    private void OutputToggleValueChanged(Toggle toggle, bool value, string outputName)
    {
        Debug.Log("CHANGED " + toggle.GetInstanceID() + " " + outputName + " " + value);
        this.UpdateRegisteredOutputs();
        this.LoadNetwork(this.networkFile);
    }

    /// <summary>
    /// Generates UI toggles for choosing inputs and outputs available in the simulation.
    /// </summary>
    private void GenerateInputOutputToggles()
    {
        // inputs
        for (int i = 0; i < CarController.PossibleInputs.Count; i++)
        {
            // instantiating toggle prefab
            GameObject newToggle = Instantiate(this.togglePrefab);
            newToggle.transform.SetParent(this.transform.Find("Select Inputs"));

            // related input name will be tied to it
            TextProperty inputName = newToggle.GetComponent<TextProperty>();
            inputName.Text = CarController.PossibleInputs[i];

            // setting position on canvas
            RectTransform rectTransformComponent = newToggle.GetComponent<RectTransform>();
            rectTransformComponent.anchoredPosition = new Vector2(0.0f, (i * -rectTransformComponent.sizeDelta.y) - 20f);

            // event listener
            Toggle toggleComponent = newToggle.GetComponent<Toggle>();
            toggleComponent.onValueChanged.AddListener(arg0 =>
            {
                this.InputToggleValueChanged(toggleComponent, toggleComponent.isOn, toggleComponent.gameObject.GetComponent<TextProperty>().Text);
            });

            // label text
            TextMeshProUGUI labelText = newToggle.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            labelText.text = CarController.PossibleInputs[i];

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
                this.OutputToggleValueChanged(toggleComponent, toggleComponent.isOn, toggleComponent.gameObject.GetComponent<TextProperty>().Text);
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

    private void GenerateSettingsUIControls(List<Setting> settings, string parentName)
    {
        for (int i = 0; i < settings.Count; i++)
        {
            // creating and positioning label text
            GameObject labelTextObject = Instantiate(this.textPrefab);
            labelTextObject.transform.SetParent(this.transform.Find(parentName));
            RectTransform labelTextRectTransform = labelTextObject.GetComponent<RectTransform>();
            labelTextRectTransform.anchoredPosition = new Vector2(0.0f, (i * -labelTextRectTransform.sizeDelta.y) - 20f);
            TextMeshProUGUI labelText = labelTextObject.GetComponent<TextMeshProUGUI>();
            labelText.text = settings[i].Name;
            labelText.alignment = TextAlignmentOptions.MidlineRight;

            // choosing which UI control to create
            GameObject newUIControl = null;
            if (settings[i].GetType() == typeof(IntSetting))
            {
                newUIControl = Instantiate(this.intFieldPrefab);
                TMP_InputField inputField = newUIControl.GetComponent<TMP_InputField>();
                inputField.text = ((IntSetting)settings[i]).Value.ToString();
            }
            if (settings[i].GetType() == typeof(FloatSetting))
            {
                newUIControl = Instantiate(this.floatFieldPrefab);
                TMP_InputField inputField = newUIControl.GetComponent<TMP_InputField>();
                inputField.text = ((FloatSetting)settings[i]).Value.ToString();
            }
            if (settings[i].GetType() == typeof(BoolSetting))
            {
                newUIControl = Instantiate(this.settingsTogglePrefab);
                Toggle toggle = newUIControl.GetComponent<Toggle>();
                toggle.isOn = ((BoolSetting)settings[i]).Value;
            }
            if (settings[i].GetType() == typeof(ChoiceSetting))
            {
                newUIControl = Instantiate(this.dropdownPrefab);
                TMP_Dropdown dropdown = newUIControl.GetComponent<TMP_Dropdown>();
                ChoiceSetting setting = (ChoiceSetting)settings[i];
                dropdown.AddOptions(setting.Choices);
                dropdown.value = setting.Value;
            }

            settings[i].Control = newUIControl;
            SettingsList.Add(settings[i]);
            newUIControl.transform.SetParent(labelTextObject.transform, false);

            // related input name will be tied to it
            TextProperty settingName = newUIControl.GetComponent<TextProperty>();
            settingName.Text = settings[i].Name;

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
        foreach (Neuron inputNeuron in SelectedNeuralNetwork.GetInputNeurons())
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
        foreach (Neuron outputNeuron in SelectedNeuralNetwork.GetOutputNeurons())
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
    /// Loads neural network from file and updates string with input/output count.
    /// </summary>
    /// <param name="filename">Name of the file.</param>
    private void LoadNetwork(string filename)
    {
        if (this.networkFile == string.Empty)
        {
            return;
        }

        SelectedNeuralNetwork = NeuralNetwork.LoadFromFile(filename);

        // inputs/outputs in the network might not match inputs/outputs in the simulation, so neural network has to be updated
        this.UpdateInputsOutputs();
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
        foreach (Setting settingFromConfig in config.Settings)
        {
            GameObject control = (from settingFromSettingsList
                                  in SettingsList
                                  where settingFromSettingsList.Name == settingFromConfig.Name
                                  select settingFromSettingsList.Control).First();
            if (control == null)
            {
                continue;
            }
            switch (settingFromConfig.GetType().Name)
            {
                case nameof(IntSetting): control.GetComponent<TMP_InputField>().text = ((IntSetting)settingFromConfig).Value.ToString();     break;
                case nameof(FloatSetting): control.GetComponent<TMP_InputField>().text = ((FloatSetting)settingFromConfig).Value.ToString(); break;
                case nameof(BoolSetting): control.GetComponent<Toggle>().isOn = ((BoolSetting)settingFromConfig).Value;                      break;
                case nameof(ChoiceSetting): control.GetComponent<TMP_Dropdown>().value = ((ChoiceSetting)settingFromConfig).Value;           break;
                default: Debug.Log(string.Format("Setting type {0} is unknown", settingFromConfig.GetType().Name));                          break;
            }
        }
    }

    /// <summary>
    /// Saves values set by UI controls to config file.
    /// </summary>
    private void SaveConfig()
    {
        Config config = new Config(SettingsList);
        config.Save();
    }

    /// <summary>
    /// Represents UI control with its value.
    /// </summary>
    [DataContract(Name = "Setting")]
    [KnownType(typeof(IntSetting))]
    [KnownType(typeof(FloatSetting))]
    [KnownType(typeof(BoolSetting))]
    [KnownType(typeof(ChoiceSetting))]
    public class Setting
    {
        [DataMember]
        private string name;

        /// <summary>
        /// Initializes a new instance of the <see cref="Setting"/> class.
        /// </summary>
        /// <param name="name">Name of the setting.</param>
        public Setting(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Name of the setting.
        /// </summary>
        public string Name { get => this.name; set => this.name = value; }

        /// <summary>
        /// UI control associated with the setting.
        /// </summary>
        public GameObject Control { get; set; }
    }

    /// <summary>
    /// Represents integer input field UI control and it's value.
    /// </summary>
    [DataContract(Name = "IntSetting")]
    public class IntSetting : Setting
    {
        [DataMember]
        private int value;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntSetting"/> class.
        /// </summary>
        /// <param name="name">Name of the setting.</param>
        /// <param name="value">Value of the setting.</param>
        public IntSetting(string name, int value)
            : base(name)
        {
            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Value of the setting.
        /// </summary>
        public int Value { get => this.value; set => this.value = value; }
    }

    /// <summary>
    /// Represents float input field UI control and it's value.
    /// </summary>
    [DataContract(Name = "FloatSetting")]
    public class FloatSetting : Setting
    {
        [DataMember]
        private float value;

        /// <summary>
        /// Initializes a new instance of the <see cref="FloatSetting"/> class.
        /// </summary>
        /// <param name="name">Name of the setting.</param>
        /// <param name="value">Value of the setting.</param>
        public FloatSetting(string name, float value)
            : base(name)
        {
            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Value of the setting.
        /// </summary>
        public float Value { get => this.value; set => this.value = value; }
    }

    /// <summary>
    /// Represents toggle UI control and it's value.
    /// </summary>
    [DataContract(Name = "BoolSetting")]
    public class BoolSetting : Setting
    {
        [DataMember]
        private bool value;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoolSetting"/> class.
        /// </summary>
        /// <param name="name">Name of the setting.</param>
        /// <param name="value">Value of the setting.</param>
        public BoolSetting(string name, bool value)
            : base(name)
        {
            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Value of the setting.
        /// </summary>
        public bool Value { get => this.value; set => this.value = value; }
    }

    /// <summary>
    /// Represents dropdown UI control and it's value.
    /// </summary>
    [DataContract(Name = "ChoiceSetting")]
    public class ChoiceSetting : Setting
    {
        [DataMember]
        private int value;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChoiceSetting"/> class.
        /// </summary>
        /// <param name="name">Name of the setting.</param>
        /// <param name="choices">List of choices in the dropdown.</param>
        /// <param name="value">Value of the setting.</param>
        public ChoiceSetting(string name, List<string> choices, int value)
            : base(name)
        {
            this.Name = name;
            this.Choices = choices;
            this.Value = value;
        }

        /// <summary>
        /// Value of the setting.
        /// </summary>
        public int Value { get => this.value; set => this.value = value; }

        /// <summary>
        /// List of choices availbale in the dropdown.
        /// </summary>
        public List<string> Choices { get; set; }
    }

    /// <summary>
    /// Class for saving/loading settings from config file.
    /// </summary>
    [DataContract(Name = "Config")]
    public class Config
    {
        private const string ConfigPath = "config.xml";

        [DataMember]
        private List<Setting> settings = new List<Setting>();
        [DataMember]
        private List<string> enabledInputList = new List<string>();
        [DataMember]
        private List<string> enabledOutputList = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Config"/> class.
        /// </summary>
        /// <param name="settings">List of settings.</param>
        public Config(List<Setting> settings)
        {
            this.Settings = settings;
        }

        /// <summary>
        /// List of settings.
        /// </summary>
        public List<Setting> Settings { get => this.settings; set => this.settings = value; }

        /// <summary>
        /// List of enabled inputs.
        /// </summary>
        public List<string> EnabledInputList { get => this.enabledInputList; set => this.enabledInputList = value; }

        /// <summary>
        /// List of enabled outputs.
        /// </summary>
        public List<string> EnabledOutputList { get => this.enabledOutputList; set => this.enabledOutputList = value; }

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
