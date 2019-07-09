using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Xml;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public enum NeuronType
{
    InputNeuron,
    OutputNeuron,
    HiddenNeuron
}

public class NeuralNetworkException : Exception
{
    public NeuralNetworkException() { }
    public NeuralNetworkException(string message) : base(message) { }
    public NeuralNetworkException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Represents input link of a neuron.
/// Needed because weights are tied to specific neurons.
/// </summary>
[Serializable]
[DataContract(Name = "InputLink")]
public class InputLink
{

    public InputLink(Neuron link, double weight)
    {
        this.link = link;
        this.weight = weight;
    }

    [DataMember]
    public Neuron link;
    [DataMember]
    public double weight;
}

/// <summary>
/// Class for neuron in neural network.
/// </summary>
[Serializable]
[DataContract(Name = "Neuron", IsReference = true)]
public class Neuron
{

    public double value = 0;                                    //used by other neurons connected to this one
    public int layerId = 0;                                     //id of layer in neural network, is assigned when sorting

    [DataMember]
    public int id = 0;
    [DataMember]
    public string name;
    [DataMember]
    public NeuronType type;                                     //whether is is input, output or hidden neuron
    [DataMember]
    public List<InputLink> inputLinks = new List<InputLink>();
    [DataMember]
    public List<Neuron> outputLinks = new List<Neuron>();
    [DataMember]
    public double bias;

    /// <summary>
    /// Parameterless contructor, needed for loading from file
    /// </summary>
    public Neuron() { }

    public Neuron(int id, string name, NeuronType type, double bias = 0)
    {
        this.id = id;
        this.name = name;
        this.type = type;
        this.bias = bias;
    }

    /// <summary>
    /// Calculates value of the neuron.
    /// </summary>
    public void Feedforward()
    {

        //if it is input neuron, value is already set by SetInputs method of NeuralNetwork class
        if (type == NeuronType.InputNeuron)
        {
            return;
        }

        //collecting inputs from connected neurons
        List<double> inputs = new List<double>();
        foreach (Neuron neuron in GetIncomingNeurons())
        {
            inputs.Add(neuron.value);
        }

        //multiplying inputs by weights
        double total = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            total += inputs[i] * inputLinks[i].weight;
        }

        //adding bias
        total += bias;

        //activation function
        double output = Math.Tanh(total);

        //result is stored in the neuron, so other neurons can use it
        value = output;

    }

    /// <summary>
    /// Randomly mutates weights and biases of the neuron.
    /// </summary>
    /// <param name="power">How likely small mutations are.</param>
    /// <param name="maxMutation">Maximum amount of mutation.</param>
    public void Mutate(double power, double maxMutation)
    {

        //input neurons don't have weights and biases, so there is nothing to mutate
        if (type == NeuronType.InputNeuron)
        {
            throw new NeuralNetworkException("Cannot mutate InputNeuron");
        }

        //mutating weights
        for (int i = 0; i < inputLinks.Count; i++)
        {
            double weightMutationRate = Math.Pow(Utils.Rand(), power) * maxMutation;
            inputLinks[i].weight += Utils.RandBetween(-weightMutationRate, weightMutationRate);
        }

        //mutating bias
        double biasMutationRate = Math.Pow(Utils.Rand(), power) * maxMutation;
        bias += Utils.RandBetween(-biasMutationRate, biasMutationRate);

    }

    /// <summary>
    /// Returns all weights of a neuron.
    /// </summary>
    public List<double> GetWeights()
    {
        return (from inputLink in inputLinks select inputLink.weight).ToList();
    }

    /// <summary>
    /// Sets weights of a neuron.
    /// </summary>
    /// <param name="newWeights">List of new weights.</param>
    public void SetWeights(List<double> newWeights)
    {
        for (int i = 0; i < newWeights.Count; i++)
        {
            inputLinks[i].weight = newWeights[i];
        }
    }

    /// <summary>
    /// Set specific weight of a neuron.
    /// </summary>
    /// <param name="name">Name of neuron tied to the weight.</param>
    /// <param name="weight">New weight.</param>
    public void SetWeight(string name, double weight)
    {
        List<InputLink> matchingInputLinks = inputLinks.FindAll((x) => x.link.name == name);
        if (matchingInputLinks.Count == 0)
        {
            throw new NeuralNetworkException(String.Format("Input link with name {0} not found", name));
        }
        else if (matchingInputLinks.Count > 1)
        {
            throw new NeuralNetworkException(String.Format("Found multiple input links with name {0}", name));
        }
        else
        {
            matchingInputLinks[0].weight = weight;
        }
    }

    /// <summary>
    /// Returns list of all neurons which are feeding values to this neuron.
    /// </summary>
    public List<Neuron> GetIncomingNeurons()
    {
        return (from inputLink in inputLinks select inputLink.link).ToList();
    }

    /// <summary>
    /// Converts neuron to a string.
    /// </summary>
    /// <returns>String representation of the neuron.</returns>
    public override string ToString()
    {
        if (type == NeuronType.InputNeuron)
        {
            return String.Format("{0} -> {1}", name, value);
        }
        string weightsString = "[";
        for (int i = 0; i < inputLinks.Count; i++)
        {
            weightsString += inputLinks[i].weight.ToString();
            if (i < inputLinks.Count - 1)
            {
                weightsString += " ";
            }
        }
        weightsString += "]";
        return String.Format("{0} {1}({2}) -> {3}", name, weightsString, bias, value);
    }

}

/// <summary>
/// Class for neural network.
/// </summary>
[Serializable]
[DataContract(Name = "NeuralNetwork")]
public class NeuralNetwork
{

    public List<List<Neuron>> layers = new List<List<Neuron>>();    //neurons sorted in layers

    [DataMember]
    private static int networkIdCounter = 0;
    [DataMember]
    public int id = 0;
    [DataMember]
    public int parent1Id = -1;
    [DataMember]
    public int parent2Id = -1;
    [DataMember]
    public double fitness = 0;
    [DataMember]
    public int breakthroughCount = 0;
    [DataMember]
    public List<Neuron> allNeurons = new List<Neuron>();

    /// <summary>
    /// Parameterless contructor, needed for loading from file
    /// </summary>
    public NeuralNetwork() { }

    /// <param name="inputCount">Number of input neurons.</param>
    /// <param name="outputCount">Number of output neurons.</param>
    /// <param name="hiddenLayers">Number of hidden layers.</param>
    /// <param name="neuronsInLayer">Numbers of neurons in each hidden layer.</param>
    public NeuralNetwork(List<string> inputNames, List<string> outputNames, int hiddenLayers, int neuronsInLayer)
    {

        //assigning id
        id = networkIdCounter;
        networkIdCounter++;

        //adding input neurons
        List<Neuron> temporaryInputNeuronList = new List<Neuron>();
        foreach (string name in inputNames)
        {
            temporaryInputNeuronList.Add(AddInputNeuron(name));
        }

        //adding hidden neurons and connecting them between each other and input neurons
        List<Neuron> previousLayer = new List<Neuron>();
        List<Neuron> currentLayer = new List<Neuron>();
        List<Neuron> temporaryHiddenNeuronList = new List<Neuron>();
        for (int layer_i = 0; layer_i < hiddenLayers; layer_i++)
        {

            currentLayer.Clear();

            //creating new layer of neurons
            for (int neuron_i = 0; neuron_i < neuronsInLayer; neuron_i++)
            {
                Neuron newNeuron = AddHiddenNeuron(String.Format("h{0}:{1}", layer_i, neuron_i));
                temporaryHiddenNeuronList.Add(newNeuron);
                currentLayer.Add(newNeuron);
            }

            //connecting first hidden layer to input neurons
            if (layer_i == 0)
            {
                foreach (Neuron inputNeuron in temporaryInputNeuronList)
                {
                    foreach (Neuron hiddenNeuron in temporaryHiddenNeuronList)
                    {
                        Connect(inputNeuron, hiddenNeuron);
                    }
                }
            }

            //connecting current layer of hidden neurons to previous one
            if (layer_i > 0)
            {
                foreach (Neuron previousLayerNeuron in previousLayer)
                {
                    foreach (Neuron currentLayerNeuron in currentLayer)
                    {
                        Connect(previousLayerNeuron, currentLayerNeuron);
                    }
                }
            }

            //swapping layers
            previousLayer = new List<Neuron>(currentLayer);

        }

        //adding output neurons
        List<Neuron> temporaryOutputNeuronList = new List<Neuron>();
        foreach (string name in outputNames)
        {
            temporaryOutputNeuronList.Add(AddOutputNeuron(name));
        }

        //connecting output neurons to last layer of hidden neurons
        foreach (Neuron hiddenNeuron in currentLayer)
        {
            foreach (Neuron outputNeuron in temporaryOutputNeuronList)
            {
                Connect(hiddenNeuron, outputNeuron);
            }
        }

    }

    /// <summary>
    /// Connects two neurons.
    /// </summary>
    /// <param name="neuron1">First neuron, cannot be OutputNeuron.</param>
    /// <param name="neuron2">Second neuron, cannot be InputNeuron.</param>
    /// <returns>InputLink of seocnd neuron.</returns>
    private InputLink Connect(Neuron neuron1, Neuron neuron2)
    {

        if (neuron1.type == NeuronType.OutputNeuron)
        {
            throw new NeuralNetworkException("Output neuron cannot have output links");
        }
        if (neuron2.type == NeuronType.InputNeuron)
        {
            throw new NeuralNetworkException("Input neuron cannot have input links");
        }

        InputLink newInputLink = new InputLink(neuron1, 0.0);
        neuron2.inputLinks.Add(newInputLink);
        neuron1.outputLinks.Add(neuron2);

        //since network structure changed, neurons have to be sorted
        SortNeurons();

        return newInputLink;

    }

    /// <summary>
    /// Adds new input neuron to the neural network (and, optionally, connect it to the next layer).
    /// </summary>
    /// <param name="name">Name of the neuron.</param>
    /// <param name="connect">If new neuron should be connected to hidden neurons right away.</param>
    /// <returns>Created neuron.</returns>
    public Neuron AddInputNeuron(string name, bool connect = false)
    {

        //creating new neuron
        Neuron newNeuron = new Neuron(allNeurons.Count, name, NeuronType.InputNeuron);

        //adding it to list
        allNeurons.Add(newNeuron);

        //connecting it to the next layer
        if (connect)
        {

            if (layers.Count < 2)
            {
                throw new NeuralNetworkException("New input neuron can be connected only if there are at least 2 layers (including input and output layers) in the network");
            }
            foreach (Neuron hiddenNeuron in layers[1])
            {
                Connect(newNeuron, hiddenNeuron);
            }


        }

        //since network structure changed, neurons have to be sorted
        SortNeurons();

        return newNeuron;

    }

    /// <summary>
    /// Adds new hidden neuron to the neural network.
    /// </summary>
    /// <param name="name">Name of the neuron.</param>
    /// <returns>Created neuron.</returns>
    private Neuron AddHiddenNeuron(string name)
    {

        Neuron newNeuron = new Neuron(allNeurons.Count, name, NeuronType.HiddenNeuron);
        allNeurons.Add(newNeuron);

        //since network structure changed, neurons have to be sorted
        SortNeurons();

        return newNeuron;

    }

    /// <summary>
    /// Adds new output neuron to the neural network.
    /// </summary>
    /// <param name="name">Name of the neuron.</param>
    /// <returns>Created neuron.</returns>
    private Neuron AddOutputNeuron(string name)
    {

        Neuron newNeuron = new Neuron(allNeurons.Count, name, NeuronType.OutputNeuron);
        allNeurons.Add(newNeuron);

        //since network structure changed, neurons have to be sorted
        SortNeurons();

        return newNeuron;

    }

    /// <summary>
    /// Removes input neuron from the network, with all related connections.
    /// </summary>
    /// <param name="name">Name of the neuron to be removed.</param>
    public void RemoveInputNeuron(string name)
    {

        Neuron inputNeuron = GetNeuronByName(name);

        //removing links in hidden neurons
        foreach (Neuron hiddenNeuron in inputNeuron.outputLinks)
        {
            hiddenNeuron.inputLinks.RemoveAll((x) => x.link == inputNeuron);
        }

        //removing neuron from lists
        allNeurons.Remove(inputNeuron);

        //since network structure changed, neurons have to be sorted
        SortNeurons();

    }

    /// <summary>
    /// Sorts neurons in layers.
    /// Field "layers" is updated as a result.
    /// </summary>
    public void SortNeurons()
    {

        //all ids have to be zero at the start so algorithm can work
        foreach (Neuron neuron in allNeurons)
        {
            neuron.layerId = 0;
        }

        //finding input neurons (they don't have any input links)
        List<Neuron> BFSlist = (from neuron in allNeurons where neuron.inputLinks.Count == 0 select neuron).ToList();

        //counting layers
        int maxLayerId = 0;

        //traversing neurons to assign layer id to them
        while (BFSlist.Count > 0)
        {
            //next step of BFS algorithm
            List<Neuron> nextList = new List<Neuron>();
            foreach (Neuron neuron in BFSlist)
            {
                //neurons which are further from input neurons will get higher layer id
                int nextLayerId = neuron.layerId + 1;
                foreach (Neuron nextNeuron in neuron.outputLinks)
                {
                    if (nextNeuron.layerId < nextLayerId)
                    {
                        //if layer id of next neuron is lower than it should be, it is updated
                        nextNeuron.layerId = nextLayerId;
                        maxLayerId = nextLayerId > maxLayerId ? nextLayerId : maxLayerId;
                    }
                    if (!nextList.Contains(nextNeuron))
                    {
                        nextList.Add(nextNeuron);
                    }
                }
            }
            //going to the next step
            BFSlist = new List<Neuron>(nextList);
        }

        //after layer ids are assigned, list of layers is formed
        layers = new List<List<Neuron>>();
        for (int i = 0; i < maxLayerId + 1; i++)
        {
            List<Neuron> layer = (from neuron in allNeurons where neuron.layerId == i select neuron).ToList();
            layers.Add(layer);
        }

    }

    /// <summary>
    /// Calculates value of all neurons in the network and returns values of output neurons.
    /// </summary>
    /// <param name="inputs">Inputs to be fed to the network.</param>
    /// <returns>List of values of output neurons.</returns>
    public Dictionary<string, double> Feedforward(Dictionary<string, double> inputs)
    {

        //setting values of input neurons
        SetInputs(inputs);

        //calculating values of neurons layer by layer
        foreach (List<Neuron> layer in layers)
        {
            foreach (Neuron neuron in layer)
            {
                neuron.Feedforward();
            }
        }

        //collecting values of output neurons
        Dictionary<string, double> output = new Dictionary<string, double>();
        foreach (Neuron neuron in GetOutputNeurons())
        {
            output.Add(neuron.name, neuron.value);
        }
        return output;

    }

    /// <summary>
    /// Sets values of input neurons.
    /// </summary>
    /// <param name="inputs">Dictionary with names of inputs and their values.</param>
    private void SetInputs(Dictionary<string, double> inputs)
    {

        if (inputs.Count != GetInputNeurons().Count)
        {
            throw new NeuralNetworkException(String.Format("Input count mismatch: {0} inputs provided while network has {1}", inputs.Count, GetInputNeurons().Count));
        }

        foreach (KeyValuePair<string, double> input in inputs)
        {
            GetNeuronByName(input.Key).value = input.Value;
        }
    }

    /// <summary>
    /// Mutates weights and biases of hidden and output neurons.
    /// </summary>
    /// <param name="power">How likely small mutations are.</param>
    /// <param name="maxMutation">Maximum amount of mutation.</param>
    public void Mutate(double power, double maxMutation)
    {
        foreach (Neuron neuron in GetHiddenNeurons())
        {
            neuron.Mutate(power, maxMutation);
        }
        foreach (Neuron neuron in GetOutputNeurons())
        {
            neuron.Mutate(power, maxMutation);
        }
    }

    /// <summary>
    /// Returns neuron with specified id.
    /// </summary>
    /// <exception cref="NeuralNetworkException">Throws if neuron not found or if there are multiple neurons with specified id.</exception>
    public Neuron GetNeuronById(int id)
    {
        List<Neuron> matchingNeurons = allNeurons.FindAll((x) => x.id == id);
        if (matchingNeurons.Count == 0)
        {
            throw new NeuralNetworkException(String.Format("Neuron with id {0} not found", id));
        }
        else if (matchingNeurons.Count > 1)
        {
            throw new NeuralNetworkException(String.Format("Found multiple neurons with id {0}", id));
        }
        else
        {
            return matchingNeurons[0];
        }
    }

    /// <summary>
    /// Returns neuron with specified name.
    /// </summary>
    /// <exception cref="NeuralNetworkException">Throws if neuron not found or if there are multiple neurons with specified name.</exception>
    public Neuron GetNeuronByName(string name)
    {
        List<Neuron> matchingNeurons = allNeurons.FindAll((x) => x.name == name);
        if (matchingNeurons.Count == 0)
        {
            throw new NeuralNetworkException(String.Format("Neuron with name {0} not found", name));
        }
        else if (matchingNeurons.Count > 1)
        {
            throw new NeuralNetworkException(String.Format("Found multiple neurons with name {0}", name));
        }
        else
        {
            return matchingNeurons[0];
        }
    }

    /// <summary>
    /// Returns list of input neurons.
    /// </summary>
    public List<Neuron> GetInputNeurons()
    {
        if (layers.Count > 0)
        {
            return layers[0];
        }
        else
        {
            return new List<Neuron>();
        }
    }

    /// <summary>
    /// Returns list of hidden neurons.
    /// </summary>
    public List<Neuron> GetHiddenNeurons()
    {
        if (layers.Count > 2)
        {
            return layers.GetRange(1, layers.Count - 2).SelectMany(i => i).ToList();
        }
        else
        {
            return new List<Neuron>();
        }
    }

    /// <summary>
    /// Returns list of output neurons.
    /// </summary>
    /// <returns></returns>
    public List<Neuron> GetOutputNeurons()
    {
        if (layers.Count > 0)
        {
            return layers.Last();
        }
        else
        {
            return new List<Neuron>();
        }
    }

    /// <summary>
    /// Converts neural network to a string.
    /// </summary>
    /// <returns>String representation of the network.</returns>
    public override string ToString()
    {
        string s = String.Format("id: {0}\n", id);
        for (int i = 0; i < layers.Count; i++)
        {
            List<Neuron> layer = layers[i];
            s += String.Format("Layer {0}\n", i);
            foreach (Neuron neuron in layer)
            {
                s += String.Format("    {0}\n", neuron.ToString());
            }
        }
        return s;
    }

    /// <summary>
    /// Copies neural network. Copy receives higher id than original.
    /// </summary>
    /// <returns></returns>
    public NeuralNetwork Copy()
    {
        NeuralNetwork copy = Utils.ObjectCopier.Clone<NeuralNetwork>(this);
        copy.id++;
        return copy;
    }

    /// <summary>
    /// Saves neural network to XML file.
    /// </summary>
    public void Serialize(String fileName)
    {
        var settings = new XmlWriterSettings();
        settings.Indent = true;
        settings.IndentChars = "    ";
        XmlWriter writer = XmlWriter.Create(fileName, settings);
        DataContractSerializer ser = new DataContractSerializer(typeof(NeuralNetwork));
        ser.WriteObject(writer, this);
        writer.Close();
    }

    /// <summary>
    /// Loads neural network from XML file.
    /// </summary>
    /// <returns>Loaded neural network.</returns>
    public static NeuralNetwork Deserialize(String fileName)
    {

        FileStream fs = new FileStream(fileName, FileMode.Open);
        XmlDictionaryReaderQuotas quotas = new XmlDictionaryReaderQuotas();
        quotas.MaxDepth = 256;
        XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(fs, quotas);
        DataContractSerializer ser = new DataContractSerializer(typeof(NeuralNetwork));
        NeuralNetwork deserializedNetwork = (NeuralNetwork)ser.ReadObject(reader, true);
        reader.Close();
        fs.Close();

        //deserializer doesn't use usual means of creating object, so this has to be done
        deserializedNetwork.SortNeurons();

        return deserializedNetwork;

    }

    /// <summary>
    /// Saves neural network to txt file.
    /// </summary>
    public void SaveToFile(string filename)
    {

        StreamWriter writer = new StreamWriter(filename);

        //saving network properties
        writer.WriteLine("id " + id);
        writer.WriteLine("fitness " + fitness);
        writer.WriteLine("breakthroughCount " + breakthroughCount);
        writer.WriteLine();

        //saving neurons
        foreach (Neuron neuron in allNeurons)
        {
            string typeString = "";
            switch (neuron.type)
            {
                case NeuronType.InputNeuron: typeString = "Input"; break;
                case NeuronType.HiddenNeuron: typeString = "Hidden"; break;
                case NeuronType.OutputNeuron: typeString = "Output"; break;
            }
            writer.WriteLine(typeString + " " + neuron.id + " " + neuron.name);
            foreach (InputLink inputLink in neuron.inputLinks)
            {
                writer.WriteLine(String.Format("    w {0} {1}", inputLink.weight, inputLink.link.id));
            }
            writer.WriteLine("    b " + neuron.bias);
        }

        writer.Close();

    }

    /// <summary>
    /// Loads neural network from txt file.
    /// </summary>
    public static NeuralNetwork LoadFromFile(string filename)
    {

        StreamReader reader = new StreamReader(filename);
        NeuralNetwork loadedNetwork = new NeuralNetwork();

        //loading network properties
        loadedNetwork.id = Int32.Parse(reader.ReadLine().Split(' ')[1]);
        loadedNetwork.fitness = Double.Parse(reader.ReadLine().Split(' ')[1]);
        loadedNetwork.breakthroughCount = Int32.Parse(reader.ReadLine().Split(' ')[1]);

        //these properties will be skipped on the second pass, so we need to know amount of them
        const int networkParameterCount = 3;

        //skipping empty line
        reader.ReadLine();

        //first pass, loading list of neurons and creating them
        while (true)
        {

            string nextLine = reader.ReadLine();

            //checking if list has ended
            if (nextLine == null || nextLine == "")
            {
                break;
            }

            //skipping lines with weights
            if (nextLine.StartsWith("    "))
            {
                continue;
            }

            //creating new neuron
            Neuron newNeuron = null;
            string[] neuronHeader = nextLine.Split(' ');
            switch (neuronHeader[0])
            {
                case "Input": newNeuron = loadedNetwork.AddInputNeuron(""); break;
                case "Hidden": newNeuron = loadedNetwork.AddHiddenNeuron(""); break;
                case "Output": newNeuron = loadedNetwork.AddOutputNeuron(""); break;
            }
            newNeuron.id = Int32.Parse(neuronHeader[1]);
            newNeuron.name = neuronHeader[2];

        }

        //reset position to the beginning of the file
        reader.DiscardBufferedData();
        reader.BaseStream.Seek(0, SeekOrigin.Begin);

        //skipping network parameters and empty line in the beginning
        for (int i = 0; i < networkParameterCount + 1; i++)
        {
            reader.ReadLine();
        }

        //second pass, loading neurons
        while (true)
        {

            //cheking if list has ended
            string nextLine = reader.ReadLine();
            if (nextLine == null || nextLine == "")
            {
                break;
            }

            //finding neuron
            string[] neuronHeader = nextLine.Split(' ');
            int neuronId = Int32.Parse(neuronHeader[1]);
            Neuron neuron = loadedNetwork.GetNeuronById(neuronId);

            //loading weights and bias
            while (true)
            {
                string[] weightOrBiasLine = reader.ReadLine().Trim().Split(' ');
                string label = weightOrBiasLine[0];
                double value = Double.Parse(weightOrBiasLine[1]);
                if (label == "w")
                {
                    int connection = Int32.Parse(weightOrBiasLine[2]);
                    Neuron anotherNeuron = loadedNetwork.GetNeuronById(connection);
                    InputLink newInputLink = loadedNetwork.Connect(anotherNeuron, neuron);
                    newInputLink.weight = value;
                }
                else if (label == "b")
                {
                    neuron.bias = value;
                    break; //line with bias is the last line of the neuron
                }
            }

        }

        return loadedNetwork;

    }

}

/// <summary>
/// Some helpful functions.
/// </summary>
public class Utils
{

    //random number generator is initialized on the start of the program
    private static Random random = new Random();

    /// <summary>
    /// Returns random double.
    /// </summary>
    public static double Rand()
    {
        return random.NextDouble();
    }

    /// <summary>
    /// Returns random double from in range from a to b.
    /// </summary>
    public static double RandBetween(double a, double b)
    {
        return random.NextDouble() * (b - a) + a;
    }

    /// <summary>
    /// Takes two lists and averages numbers in pairs.
    /// </summary>
    /// <returns>List with averaged numbers.</returns>
    public static List<double> ListsAverage(List<double> list1, List<double> list2)
    {
        List<double> avgList = new List<double>();
        for (int i = 0; i < list1.Count; i++)
        {
            avgList.Add((list1[i] + list2[i]) / 2.0);
        }
        return avgList;
    }

    /// <summary>
    /// Converts value in one range to another range.
    /// </summary>
    /// <param name="val">Value in the first range which is going to be converted to another range.</param>
    /// <param name="min1">Start of first range.</param>
    /// <param name="max1">End of first range.</param>
    /// <param name="min2">Start of second range.</param>
    /// <param name="max2">End of second range.</param>
    /// <param name="clamp">If <c>val</c> is not in the first range, clamp converted value to edges of second range.</param>
    /// <returns>Value in the second range.</returns>
    public static double MapRange(double val, double min1, double max1, double min2, double max2, bool clamp = false)
    {
        double range = max1 - min1;
        if (range == 0)
        {
            return 0;
        }
        double scaledRange = max2 - min2;
        double scale = scaledRange / range;
        double dist = val - min1;
        double scaledDist = dist * scale;
        double result = min2 + scaledDist;
        if (clamp)
        {
            if (result < min2)
            {
                result = min2;
            }
            if (result > max2)
            {
                result = max2;
            }
        }
        return result;
    }

    /// <summary>
    /// Reference Article http://www.codeproject.com/KB/tips/SerializedObjectCloner.aspx
    /// Provides a method for performing a deep copy of an object.
    /// Binary Serialization is used to perform the copy.
    /// </summary>
    public static class ObjectCopier
    {
        /// <summary>
        /// Perform a deep Copy of the object.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException("The type must be serializable.", "source");
            }

            // Don't serialize a null object, simply return the default for that object
            if (Object.ReferenceEquals(source, null))
            {
                return default(T);
            }

            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using (stream)
            {
                formatter.Serialize(stream, source);
                stream.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(stream);
            }
        }
    }

}

/// <summary>
/// Some testing.
/// </summary>
class Program
{
    static void Main(string[] args)
    {

        List<string> inputList = new List<string> { "input1", "input2" };
        List<string> outputList = new List<string> { "output1", "output2" };

        Console.WriteLine("Basic test");
        Console.WriteLine();

        NeuralNetwork network1 = new NeuralNetwork(inputList, outputList, 2, 2);
        network1.GetHiddenNeurons()[0].inputLinks[0].weight = 1;
        network1.GetHiddenNeurons()[2].inputLinks[0].weight = 1;
        network1.GetOutputNeurons()[0].inputLinks[0].weight = 1;
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

        network3.GetHiddenNeurons()[0].inputLinks[0].weight = -0.083920758234613382;
        network3.GetHiddenNeurons()[0].inputLinks[1].weight = -0.10348547168394251;
        network3.GetHiddenNeurons()[1].inputLinks[0].weight = 0.9864023685598281;
        network3.GetHiddenNeurons()[1].inputLinks[1].weight = 0.15295621398799475;
        network3.GetHiddenNeurons()[2].inputLinks[0].weight = -0.070737256110288152;
        network3.GetHiddenNeurons()[2].inputLinks[1].weight = -1.0365333983959844;
        network3.GetHiddenNeurons()[3].inputLinks[0].weight = 0.46109638325388069;
        network3.GetHiddenNeurons()[3].inputLinks[1].weight = -0.25149451732354522;
        network3.GetOutputNeurons()[0].inputLinks[0].weight = 0.50648030282771972;
        network3.GetOutputNeurons()[0].inputLinks[1].weight = 0.51670017869582552;
        network3.GetOutputNeurons()[1].inputLinks[0].weight = 0.23281424843657786;
        network3.GetOutputNeurons()[1].inputLinks[1].weight = -0.60550942224139681;
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

        Console.WriteLine("Adding neuron test");
        Console.WriteLine();

        network5.AddInputNeuron("newInput", true);
        network5.GetHiddenNeurons()[0].SetWeight("newInput", 0.333);
        network5.GetHiddenNeurons()[1].SetWeight("newInput", -0.333);
        network5.Feedforward(new Dictionary<string, double> { { "input1", 2 }, { "input2", 3 }, { "newInput", 5.5 } });
        Console.WriteLine(network5);
        Console.WriteLine();

        Console.WriteLine("Removing neuron test");
        Console.WriteLine();

        network5.RemoveInputNeuron("input1");
        network5.Feedforward(new Dictionary<string, double> { { "input2", 2 }, { "newInput", 3 } });
        Console.WriteLine(network5);
        Console.WriteLine();

    }
}