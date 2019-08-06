using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

/// <summary>
/// Determines type of a neuron. Some operations will throw exception if type is wrong.
/// </summary>
public enum NeuronType
{
    /// <summary>
    /// Cannot have input links.
    /// </summary>
    InputNeuron,

    /// <summary>
    /// Cannot have output links.
    /// </summary>
    OutputNeuron,

    /// <summary>
    /// Can have both types of links.
    /// </summary>
    HiddenNeuron,
}

/// <summary>
/// Class for neural network.
/// </summary>
[Serializable]
[DataContract(Name = "NeuralNetwork")]
public class NeuralNetwork
{
    private static int networkIdCounter = 0;

    [DataMember]
    private readonly List<Neuron> allNeurons = new List<Neuron>();

    /// <summary>
    /// Initializes a new instance of the <see cref="NeuralNetwork"/> class.
    /// Parameterless contructor, needed for loading from file.
    /// </summary>
    public NeuralNetwork()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NeuralNetwork"/> class.
    /// </summary>
    /// <param name="inputNames">Names of inputs.</param>
    /// <param name="outputNames">Names of outputs.</param>
    /// <param name="hiddenLayers">Number of hidden layers.</param>
    /// <param name="neuronsInLayer">Numbers of neurons in each hidden layer.</param>
    public NeuralNetwork(List<string> inputNames, List<string> outputNames, int hiddenLayers, int neuronsInLayer)
    {
        // assigning id
        this.Id = networkIdCounter;
        networkIdCounter++;

        // adding input neurons
        List<Neuron> temporaryInputNeuronList = new List<Neuron>();
        foreach (string name in inputNames)
        {
            temporaryInputNeuronList.Add(this.AddInputNeuron(name));
        }

        // adding hidden neurons and connecting them between each other and input neurons
        List<Neuron> previousLayer = new List<Neuron>();
        List<Neuron> currentLayer = new List<Neuron>();
        List<Neuron> temporaryHiddenNeuronList = new List<Neuron>();
        for (int layer_i = 0; layer_i < hiddenLayers; layer_i++)
        {
            currentLayer.Clear();

            // creating new layer of neurons
            for (int neuron_i = 0; neuron_i < neuronsInLayer; neuron_i++)
            {
                Neuron newNeuron = this.AddHiddenNeuron(string.Format("h{0}:{1}", layer_i, neuron_i));
                temporaryHiddenNeuronList.Add(newNeuron);
                currentLayer.Add(newNeuron);
            }

            // connecting first hidden layer to input neurons
            if (layer_i == 0)
            {
                foreach (Neuron inputNeuron in temporaryInputNeuronList)
                {
                    foreach (Neuron hiddenNeuron in temporaryHiddenNeuronList)
                    {
                        this.Connect(inputNeuron, hiddenNeuron);
                    }
                }
            }

            // connecting current layer of hidden neurons to previous one
            if (layer_i > 0)
            {
                foreach (Neuron previousLayerNeuron in previousLayer)
                {
                    foreach (Neuron currentLayerNeuron in currentLayer)
                    {
                        this.Connect(previousLayerNeuron, currentLayerNeuron);
                    }
                }
            }

            // swapping layers
            previousLayer = new List<Neuron>(currentLayer);
        }

        // adding output neurons
        List<Neuron> temporaryOutputNeuronList = new List<Neuron>();
        foreach (string name in outputNames)
        {
            temporaryOutputNeuronList.Add(this.AddOutputNeuron(name));
        }

        // connecting output neurons to last layer of hidden neurons
        foreach (Neuron hiddenNeuron in currentLayer)
        {
            foreach (Neuron outputNeuron in temporaryOutputNeuronList)
            {
                this.Connect(hiddenNeuron, outputNeuron);
            }
        }
    }

    /// <summary>
    /// Neurons sorted in layers.
    /// </summary>
    public List<List<Neuron>> Layers { get; set; } = new List<List<Neuron>>();

    /// <summary>
    /// List of input neurons.
    /// </summary>
    public List<Neuron> InputNeurons
    {
        get
        {
            if (this.Layers.Count > 0)
            {
                return this.Layers[0];
            }
            else
            {
                return new List<Neuron>();
            }
        }
    }

    /// <summary>
    /// List of hidden neurons.
    /// </summary>
    public List<Neuron> HiddenNeurons
    {
        get
        {
            if (this.Layers.Count > 2)
            {
                return this.Layers.GetRange(1, this.Layers.Count - 2).SelectMany(i => i).ToList();
            }
            else
            {
                return new List<Neuron>();
            }
        }
    }

    /// <summary>
    /// List of output neurons.
    /// </summary>
    public List<Neuron> OutputNeurons
    {
        get
        {
            if (this.Layers.Count > 0)
            {
                return this.Layers.Last();
            }
            else
            {
                return new List<Neuron>();
            }
        }
    }

    /// <summary>
    /// Fitness asigned by genetic algorithm. Is saved to file.
    /// </summary>
    [DataMember]
    public double Fitness { get; set; } = 0;

    /// <summary>
    /// Measure of how long netwrok has been training. Is saved to file.
    /// </summary>
    [DataMember]
    public int BreakthroughCount { get; set; } = 0;

    /// <summary>
    /// Name of the track network was training on.
    /// </summary>
    [DataMember]
    public string TrackName { get; set; } = null;

    /// <summary>
    /// Id of the network.
    /// </summary>
    [DataMember]
    public int Id { get; set; } = 0;

    /// <summary>
    /// Adds new input neuron to the neural network (and, optionally, connect it to the next layer).
    /// </summary>
    /// <param name="name">Name of the neuron.</param>
    /// <param name="connect">If new neuron should be connected to neurons in the next layer.</param>
    /// <returns>Created neuron.</returns>
    public Neuron AddInputNeuron(string name, bool connect = false)
    {
        // creating new neuron
        Neuron newNeuron = new Neuron(this.allNeurons.Count, name, NeuronType.InputNeuron);

        // adding it to list
        this.allNeurons.Add(newNeuron);

        // connecting it to the next layer
        if (connect)
        {
            if (this.Layers.Count < 2)
            {
                throw new NeuralNetworkException("New input neuron can be connected only if there are at least 2 layers (including input and output layers) in the network");
            }
            foreach (Neuron hiddenNeuron in this.Layers[1])
            {
                this.Connect(newNeuron, hiddenNeuron);
            }
        }

        // since network structure changed, neurons have to be sorted
        this.SortNeurons();

        return newNeuron;
    }

    /// <summary>
    /// Adds new output neuron to the neural network (and, optionally, connect it to the previous layer).
    /// </summary>
    /// <param name="name">Name of the neuron.</param>
    /// <param name="connect">If new neuron should be connected to neurons in the previous layer.</param>
    /// <returns>Created neuron.</returns>
    public Neuron AddOutputNeuron(string name, bool connect = false)
    {
        // creating new neuron
        Neuron newNeuron = new Neuron(this.allNeurons.Count, name, NeuronType.OutputNeuron);

        // adding it to the list
        this.allNeurons.Add(newNeuron);

        // connecting it to the previous layer
        if (connect)
        {
            if (this.Layers.Count < 2)
            {
                throw new NeuralNetworkException("New output neuron can be connected only if there are at least 2 layers (including input and output layers) in the network");
            }
            foreach (Neuron hiddenNeuron in this.Layers[this.Layers.Count - 2])
            {
                this.Connect(hiddenNeuron, newNeuron);
            }
        }

        // since network structure changed, neurons have to be sorted
        this.SortNeurons();

        return newNeuron;
    }

    /// <summary>
    /// Removes input neuron from the network, with all related connections.
    /// </summary>
    /// <param name="name">Name of the neuron to be removed.</param>
    public void RemoveInputNeuron(string name)
    {
        Neuron inputNeuron = this.GetNeuronByName(name);

        // removing links in hidden neurons
        foreach (Neuron hiddenNeuron in inputNeuron.OutputLinks)
        {
            hiddenNeuron.InputLinks.RemoveAll((x) => x.Link == inputNeuron);
        }

        // removing neuron from the list
        this.allNeurons.Remove(inputNeuron);

        // since network structure changed, neurons have to be sorted
        this.SortNeurons();
    }

    /// <summary>
    /// Removes output neuron from the network, with all related connections.
    /// </summary>
    /// <param name="neuron">Neuron to be removed.</param>
    public void RemoveOutputNeuron(Neuron neuron)
    {
        // removing links in hidden neurons
        foreach (Neuron hiddenNeuron in from inputLink in neuron.InputLinks select inputLink.Link)
        {
            hiddenNeuron.OutputLinks.Remove(neuron);
        }

        // removing neuron from the list
        this.allNeurons.Remove(neuron);

        // since network structure changed, neurons have to be sorted
        this.SortNeurons();
    }

    /// <summary>
    /// Removes output neuron from the network, with all related connections.
    /// </summary>
    /// <param name="name">Name of the neuron to be removed.</param>
    public void RemoveOutputNeuron(string name)
    {
        Neuron outputNeuron = this.GetNeuronByName(name);
        this.RemoveOutputNeuron(outputNeuron);
    }

    /// <summary>
    /// Calculates value of all neurons in the network and returns values of output neurons.
    /// </summary>
    /// <param name="inputs">Inputs to be fed to the network.</param>
    /// <returns>List of values of output neurons.</returns>
    public Dictionary<string, double> Feedforward(Dictionary<string, double> inputs)
    {
        // setting values of input neurons
        this.SetInputs(inputs);

        // calculating values of neurons layer by layer
        foreach (List<Neuron> layer in this.Layers)
        {
            foreach (Neuron neuron in layer)
            {
                neuron.Feedforward();
            }
        }

        // collecting values of output neurons
        Dictionary<string, double> output = new Dictionary<string, double>();
        foreach (Neuron neuron in this.OutputNeurons)
        {
            output.Add(neuron.Name, neuron.Value);
        }
        return output;
    }

    /// <summary>
    /// Mutates weights and biases of hidden and output neurons.
    /// </summary>
    /// <param name="power">How likely small mutations are.</param>
    /// <param name="maxMutation">Maximum amount of mutation.</param>
    public void Mutate(double power, double maxMutation)
    {
        foreach (Neuron neuron in this.HiddenNeurons)
        {
            neuron.Mutate(power, maxMutation);
        }
        foreach (Neuron neuron in this.OutputNeurons)
        {
            neuron.Mutate(power, maxMutation);
        }
    }

    /// <summary>
    /// Returns neuron with specified id. Throws exception if neuron not found, or found multiple neurons with specified id.
    /// </summary>
    /// <param name="id">Id of the neuron.</param>
    /// <returns>Found neuron.</returns>
    public Neuron GetNeuronById(int id)
    {
        List<Neuron> matchingNeurons = this.allNeurons.FindAll((x) => x.Id == id);
        if (matchingNeurons.Count == 0)
        {
            throw new KeyNotFoundException(string.Format("Neuron with id {0} not found", id));
        }
        else if (matchingNeurons.Count > 1)
        {
            throw new NeuralNetworkException(string.Format("Found multiple neurons with id {0}", id));
        }
        else
        {
            return matchingNeurons[0];
        }
    }

    /// <summary>
    /// Returns neuron with specified name. Throws exception if neuron not found, or found multiple neurons with specified name.
    /// </summary>
    /// <param name="name">Name of the neuron.</param>
    /// <returns>Found neuron.</returns>
    public Neuron GetNeuronByName(string name)
    {
        List<Neuron> matchingNeurons = this.allNeurons.FindAll((x) => x.Name == name);
        if (matchingNeurons.Count == 0)
        {
            throw new KeyNotFoundException(string.Format("Neuron with name {0} not found", name));
        }
        else if (matchingNeurons.Count > 1)
        {
            throw new NeuralNetworkException(string.Format("Found multiple neurons with name {0}", name));
        }
        else
        {
            return matchingNeurons[0];
        }
    }

    /// <summary>
    /// Converts neural network to a string.
    /// </summary>
    /// <returns>String representation of the network.</returns>
    public override string ToString()
    {
        string s = string.Format("id: {0}\n", this.Id);
        for (int i = 0; i < this.Layers.Count; i++)
        {
            List<Neuron> layer = this.Layers[i];
            s += string.Format("Layer {0}\n", i);
            foreach (Neuron neuron in layer)
            {
                s += string.Format("    {0}\n", neuron.ToString());
            }
        }
        return s;
    }

    /// <summary>
    /// Copies neural network. Copy receives higher id than original.
    /// </summary>
    /// <returns>Copy of this neural network, but with higher id.</returns>
    public NeuralNetwork Copy()
    {
        NeuralNetwork copy = Utils.ObjectCopier.Clone<NeuralNetwork>(this);
        copy.Id = networkIdCounter;
        networkIdCounter++;
        return copy;
    }

    /// <summary>
    /// Saves neural network to XML file.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    public void Serialize(string fileName)
    {
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "    ",
        };
        XmlWriter writer = XmlWriter.Create(fileName, settings);
        DataContractSerializer ser = new DataContractSerializer(typeof(NeuralNetwork));
        ser.WriteObject(writer, this);
        writer.Close();
    }

    /// <summary>
    /// Loads neural network from XML file.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <returns>Loaded neural network.</returns>
#pragma warning disable SA1204 // Static elements should appear before instance elements
    public static NeuralNetwork Deserialize(string fileName)
#pragma warning restore SA1204 // Static elements should appear before instance elements
    {
        NeuralNetwork deserializedNetwork;
        using (FileStream fs = new FileStream(fileName, FileMode.Open))
        {
            XmlDictionaryReaderQuotas quotas = new XmlDictionaryReaderQuotas
            {
                MaxDepth = 256,
            };
            XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(fs, quotas);
            DataContractSerializer ser = new DataContractSerializer(typeof(NeuralNetwork));
            deserializedNetwork = (NeuralNetwork)ser.ReadObject(reader, true);
            reader.Close();
        }

        // deserializer doesn't use usual means of creating object, so this has to be done
        deserializedNetwork.SortNeurons();

        return deserializedNetwork;
    }

    /// <summary>
    /// Saves neural network to txt file.
    /// </summary>
    /// <param name="filename">Name of the file.</param>
    public void SaveToFile(string filename)
    {
        StreamWriter writer = new StreamWriter(filename);

        // saving network properties
        writer.WriteLine("id " + this.Id);
        writer.WriteLine("fitness " + this.Fitness);
        writer.WriteLine("breakthroughCount " + this.BreakthroughCount);
        writer.WriteLine("trackName " + this.TrackName);
        writer.WriteLine();

        // saving neurons
        foreach (Neuron neuron in this.allNeurons)
        {
            string typeString = string.Empty;
            switch (neuron.Type)
            {
                case NeuronType.InputNeuron: typeString = "Input"; break;
                case NeuronType.HiddenNeuron: typeString = "Hidden"; break;
                case NeuronType.OutputNeuron: typeString = "Output"; break;
            }
            writer.WriteLine(typeString + " " + neuron.Id + " " + neuron.Name);
            foreach (Neuron.InputLink inputLink in neuron.InputLinks)
            {
                writer.WriteLine(string.Format("    w {0} {1}", inputLink.Weight, inputLink.Link.Id));
            }
            writer.WriteLine("    b " + neuron.Bias);
        }

        writer.Close();
    }

    /// <summary>
    /// Loads neural network from txt file.
    /// </summary>
    /// /// <param name="filename">Name of the file.</param>
    /// <returns>Neural network loaded from the file.</returns>
#pragma warning disable SA1204 // Static elements should appear before instance elements
    public static NeuralNetwork LoadFromFile(string filename)
#pragma warning restore SA1204 // Static elements should appear before instance elements
    {
        StreamReader reader = new StreamReader(filename);
        NeuralNetwork loadedNetwork = new NeuralNetwork
        {
            // loading network properties
            Id = int.Parse(reader.ReadLine().Split(' ')[1]),
            Fitness = double.Parse(reader.ReadLine().Split(' ')[1]),
            BreakthroughCount = int.Parse(reader.ReadLine().Split(' ')[1]),
            TrackName = reader.ReadLine().Split(' ')[1],
        };

        // these properties will be skipped on the second pass, so we need to know amount of them
        const int networkParameterCount = 4;

        // skipping empty line
        reader.ReadLine();

        // first pass, loading list of neurons and creating them
        while (true)
        {
            string nextLine = reader.ReadLine();

            // checking if list has ended
            if (nextLine == null || nextLine == string.Empty)
            {
                break;
            }

            // skipping lines with weights
            if (nextLine.StartsWith("    "))
            {
                continue;
            }

            // creating new neuron
            Neuron newNeuron = null;
            string[] neuronHeader = nextLine.Split(' ');
            switch (neuronHeader[0])
            {
                case "Input": newNeuron = loadedNetwork.AddInputNeuron(string.Empty); break;
                case "Hidden": newNeuron = loadedNetwork.AddHiddenNeuron(string.Empty); break;
                case "Output": newNeuron = loadedNetwork.AddOutputNeuron(string.Empty); break;
            }
            newNeuron.Id = int.Parse(neuronHeader[1]);
            newNeuron.Name = neuronHeader[2];
        }

        // reset position to the beginning of the file
        reader.DiscardBufferedData();
        reader.BaseStream.Seek(0, SeekOrigin.Begin);

        // skipping network parameters and empty line in the beginning
        for (int i = 0; i < networkParameterCount + 1; i++)
        {
            reader.ReadLine();
        }

        // second pass, loading neurons
        while (true)
        {
            // cheking if list has ended
            string nextLine = reader.ReadLine();
            if (nextLine == null || nextLine == string.Empty)
            {
                break;
            }

            // finding neuron
            string[] neuronHeader = nextLine.Split(' ');
            int neuronId = int.Parse(neuronHeader[1]);
            Neuron neuron = loadedNetwork.GetNeuronById(neuronId);

            // loading weights and bias
            while (true)
            {
                string[] weightOrBiasLine = reader.ReadLine().Trim().Split(' ');
                string label = weightOrBiasLine[0];
                double value = double.Parse(weightOrBiasLine[1]);
                if (label == "w")
                {
                    int connection = int.Parse(weightOrBiasLine[2]);
                    Neuron anotherNeuron = loadedNetwork.GetNeuronById(connection);
                    Neuron.InputLink newInputLink = loadedNetwork.Connect(anotherNeuron, neuron);
                    newInputLink.Weight = value;
                }
                else if (label == "b")
                {
                    neuron.Bias = value;
                    break; // line with bias is the last line of the neuron
                }
            }
        }

        return loadedNetwork;
    }

    /// <summary>
    /// Connects two neurons.
    /// </summary>
    /// <param name="neuron1">First neuron, cannot be OutputNeuron.</param>
    /// <param name="neuron2">Second neuron, cannot be InputNeuron.</param>
    /// <returns>InputLink of seocnd neuron.</returns>
    private Neuron.InputLink Connect(Neuron neuron1, Neuron neuron2)
    {
        if (neuron1.Type == NeuronType.OutputNeuron)
        {
            throw new NeuralNetworkException("Output neuron cannot have output links");
        }
        if (neuron2.Type == NeuronType.InputNeuron)
        {
            throw new NeuralNetworkException("Input neuron cannot have input links");
        }

        Neuron.InputLink newInputLink = new Neuron.InputLink(neuron1, 0.0);
        neuron2.InputLinks.Add(newInputLink);
        neuron1.OutputLinks.Add(neuron2);

        // since network structure changed, neurons have to be sorted
        this.SortNeurons();

        return newInputLink;
    }

    /// <summary>
    /// Adds new hidden neuron to the neural network.
    /// </summary>
    /// <param name="name">Name of the neuron.</param>
    /// <returns>Created neuron.</returns>
    private Neuron AddHiddenNeuron(string name)
    {
        Neuron newNeuron = new Neuron(this.allNeurons.Count, name, NeuronType.HiddenNeuron);
        this.allNeurons.Add(newNeuron);

        // since network structure changed, neurons have to be sorted
        this.SortNeurons();

        return newNeuron;
    }

    /// <summary>
    /// Sorts neurons in layers.
    /// Field "layers" is updated as a result.
    /// </summary>
    private void SortNeurons()
    {
        // all ids have to be zero at the start so algorithm can work
        foreach (Neuron neuron in this.allNeurons)
        {
            neuron.LayerId = 0;
        }

        // finding input neurons (they don't have any input links)
        List<Neuron> bfsList = (from neuron in this.allNeurons where neuron.InputLinks.Count == 0 select neuron).ToList();

        // counting layers
        int maxLayerId = 0;

        // traversing neurons to assign layer id to them
        while (bfsList.Count > 0)
        {
            // next step of BFS algorithm
            List<Neuron> nextList = new List<Neuron>();
            foreach (Neuron neuron in bfsList)
            {
                // neurons which are further from input neurons will get higher layer id
                int nextLayerId = neuron.LayerId + 1;
                foreach (Neuron nextNeuron in neuron.OutputLinks)
                {
                    if (nextNeuron.LayerId < nextLayerId)
                    {
                        // if layer id of next neuron is lower than it should be, it is updated
                        nextNeuron.LayerId = nextLayerId;
                        maxLayerId = nextLayerId > maxLayerId ? nextLayerId : maxLayerId;
                    }
                    if (!nextList.Contains(nextNeuron))
                    {
                        nextList.Add(nextNeuron);
                    }
                }
            }

            // going to the next step
            bfsList = new List<Neuron>(nextList);
        }

        // after layer ids are assigned, list of layers is formed
        this.Layers = new List<List<Neuron>>();
        for (int i = 0; i < maxLayerId + 1; i++)
        {
            List<Neuron> layer = (from neuron in this.allNeurons where neuron.LayerId == i select neuron).ToList();
            this.Layers.Add(layer);
        }
    }

    /// <summary>
    /// Sets values of input neurons.
    /// </summary>
    /// <param name="inputs">Dictionary with names of inputs and their values.</param>
    private void SetInputs(Dictionary<string, double> inputs)
    {
        if (inputs.Count != this.InputNeurons.Count)
        {
            throw new NeuralNetworkException(string.Format("Input count mismatch: {0} inputs provided while network has {1}", inputs.Count, this.InputNeurons.Count));
        }

        foreach (KeyValuePair<string, double> input in inputs)
        {
            Neuron neuron = this.GetNeuronByName(input.Key);
            if (neuron.Type != NeuronType.InputNeuron)
            {
                throw new NeuralNetworkException(string.Format("Neuron {0} is not InputNeuron", neuron.Name));
            }
            neuron.Value = input.Value;
        }
    }

    /// <summary>
    /// General exception for NeuralNetwork.
    /// </summary>
    [Serializable]
    public class NeuralNetworkException : Exception
    {
#pragma warning disable SA1600 // Elements should be documented
        public NeuralNetworkException()
        {
        }

        public NeuralNetworkException(string message)
            : base(message)
        {
        }

        public NeuralNetworkException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
#pragma warning restore SA1600 // Elements should be documented
    }
}

/// <summary>
/// Some testing.
/// </summary>
#pragma warning disable SA1402 // File may only contain a single type
internal class Program
#pragma warning restore SA1402 // File may only contain a single type
{
    private static void Main()
    {
        List<string> inputList = new List<string> { "input1", "input2" };
        List<string> outputList = new List<string> { "output1", "output2" };

        Console.WriteLine("Basic test");
        Console.WriteLine();

        NeuralNetwork network1 = new NeuralNetwork(inputList, outputList, 2, 2);
        network1.HiddenNeurons[0].InputLinks[0].Weight = 1;
        network1.HiddenNeurons[2].InputLinks[0].Weight = 1;
        network1.OutputNeurons[0].InputLinks[0].Weight = 1;
        network1.Feedforward(new Dictionary<string, double> { { "input1", 2 }, { "input2", 3 } });
        Console.WriteLine(network1);
        Console.WriteLine();

        Console.WriteLine("Copying test");
        Console.WriteLine();

        NeuralNetwork network2 = network1.Copy();
        network2.Feedforward(new Dictionary<string, double> { { "input1", 2 }, { "input2", 3 } });
        Console.WriteLine(network2);
        Console.WriteLine();

        Console.WriteLine("Random weights test");
        Console.WriteLine();

        NeuralNetwork network3 = network2.Copy();
        network3.Feedforward(new Dictionary<string, double> { { "input1", 2 }, { "input2", 3 } });
        Console.WriteLine(network3);
        Console.WriteLine();

        network3.HiddenNeurons[0].InputLinks[0].Weight = -0.083920758234613382;
        network3.HiddenNeurons[0].InputLinks[1].Weight = -0.10348547168394251;
        network3.HiddenNeurons[1].InputLinks[0].Weight = 0.9864023685598281;
        network3.HiddenNeurons[1].InputLinks[1].Weight = 0.15295621398799475;
        network3.HiddenNeurons[2].InputLinks[0].Weight = -0.070737256110288152;
        network3.HiddenNeurons[2].InputLinks[1].Weight = -1.0365333983959844;
        network3.HiddenNeurons[3].InputLinks[0].Weight = 0.46109638325388069;
        network3.HiddenNeurons[3].InputLinks[1].Weight = -0.25149451732354522;
        network3.OutputNeurons[0].InputLinks[0].Weight = 0.50648030282771972;
        network3.OutputNeurons[0].InputLinks[1].Weight = 0.51670017869582552;
        network3.OutputNeurons[1].InputLinks[0].Weight = 0.23281424843657786;
        network3.OutputNeurons[1].InputLinks[1].Weight = -0.60550942224139681;
        network3.Feedforward(new Dictionary<string, double> { { "input1", 2 }, { "input2", 3 } });
        Console.WriteLine(network3);
        Console.WriteLine();

        Console.WriteLine("xml serialization test");
        Console.WriteLine();

        network3.Serialize("network.xml");

        NeuralNetwork network4 = NeuralNetwork.Deserialize("network.xml");
        network4.Feedforward(new Dictionary<string, double> { { "input1", 2 }, { "input2", 3 } });
        Console.WriteLine(network4);
        Console.WriteLine();

        Console.WriteLine("file save/load test");
        Console.WriteLine();

        network4.SaveToFile("network.txt");

        NeuralNetwork network5 = NeuralNetwork.LoadFromFile("network.txt");
        network5.Feedforward(new Dictionary<string, double> { { "input1", 2 }, { "input2", 3 } });
        Console.WriteLine(network5);
        Console.WriteLine();

        Console.WriteLine("Adding input neuron test");
        Console.WriteLine();

        network5.AddInputNeuron("newInput", true);
        network5.HiddenNeurons[0].SetWeight("newInput", 0.333);
        network5.HiddenNeurons[1].SetWeight("newInput", -0.333);
        network5.Feedforward(new Dictionary<string, double> { { "input1", 2 }, { "input2", 3 }, { "newInput", 5.5 } });
        Console.WriteLine(network5);
        Console.WriteLine();

        Console.WriteLine("Removing input neuron test");
        Console.WriteLine();

        network5.RemoveInputNeuron("input1");
        network5.Feedforward(new Dictionary<string, double> { { "input2", 2 }, { "newInput", 3 } });
        Console.WriteLine(network5);
        Console.WriteLine();

        Console.WriteLine("Adding output neuron test");
        Console.WriteLine();

        Neuron newOutputNeuron = network5.AddOutputNeuron("newOutput", true);
        newOutputNeuron.SetWeights(new List<double> { 0.555, -0.555 });
        network5.Feedforward(new Dictionary<string, double> { { "input2", 2 }, { "newInput", 3 } });
        Console.WriteLine(network5);
        Console.WriteLine();

        Console.WriteLine("Removing output neuron test");
        Console.WriteLine();

        network5.RemoveOutputNeuron("output1");
        network5.Feedforward(new Dictionary<string, double> { { "input2", 2 }, { "newInput", 3 } });
        Console.WriteLine(network5);
        Console.WriteLine();
    }
}