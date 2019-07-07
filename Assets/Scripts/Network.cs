using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Xml;
using System.IO;

public enum NeuronType
{
    InputNeuron,
    OutputNeuron,
    HiddenNeuron
}

[Serializable]
public class WrongNeuronTypeException : Exception
{
    public WrongNeuronTypeException() { }
    public WrongNeuronTypeException(string message) : base(message) { }
    public WrongNeuronTypeException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Class for neuron in neural network.
/// </summary>
[DataContract(Name = "Neuron", IsReference = true)]
public class Neuron
{
    [DataMember]
    public int id = 0;
    [DataMember]
    public string name;
    [DataMember]
    public NeuronType type;                                 //whether is is input, output or hidden neuron
    [DataMember]
    public double value = 0;
    [DataMember]
    public List<Neuron> inputLinks = new List<Neuron>();
    [DataMember]
    public List<Neuron> outputLinks = new List<Neuron>();
    [DataMember]
    public List<double> weights = new List<double>();
    [DataMember]
    public double bias;

    /// <summary>
    /// Parameterless contructor, needed for loading from file
    /// </summary>
    public Neuron() { }

    public Neuron(int id, string name, NeuronType type, List<double> weights = null, double bias = 0)
    {
        this.id = id;
        this.name = name;
        this.type = type;
        if (weights != null)
        {
            this.weights = new List<double>(weights);
        }
        this.bias = bias;
    }

    /// <summary>
    /// Calculates values of all neurons.
    /// </summary>
    public void Feedforward()
    {

        //if it is input neuron, value is already set by SetInputs method of NeuralNetwork class
        if (type == NeuronType.InputNeuron)
        {
            return;
        }

        //collecting inputs
        List<double> inputs = new List<double>();
        foreach (Neuron neuron in inputLinks)
        {
            inputs.Add(neuron.value);
        }

        //multiplying inputs by weights
        double total = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            total += inputs[i] * weights[i];
        }

        total += bias;

        //activation function
        double output = Math.Tanh(total);

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
            throw new WrongNeuronTypeException("Cannot mutate InputNeuron");
        }

        //mutating weights
        for (int i = 0; i < weights.Count; i++)
        {
            double weightMutationRate = Math.Pow(Utils.Rand(), power) * maxMutation;
            weights[i] += Utils.RandBetween(-weightMutationRate, weightMutationRate);
        }

        //mutating bias
        double biasMutationRate = Math.Pow(Utils.Rand(), power) * maxMutation;
        bias += Utils.RandBetween(-biasMutationRate, biasMutationRate);

    }

    /// <summary>
    /// Does crossover of two neurons and saves result in another neuron.
    /// </summary>
    /// <param name="neuron">Result is saved in this neuron</param>
    /// <param name="parent1">First parent</param>
    /// <param name="parent2">Second parent</param>
    public static void NeuronCrossover(Neuron neuron, Neuron parent1, Neuron parent2)
    {
        List<double> averagedWeights = Utils.ListsAverage(parent1.weights, parent2.weights);
        double averagedBias = (parent1.bias + parent2.bias) / 2.0;
        neuron.weights = averagedWeights;
        neuron.bias = averagedBias;
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
        for (int i = 0; i < weights.Count; i++)
        {
            weightsString += weights[i].ToString();
            if (i < weights.Count - 1)
            {
                weightsString += " ";
            }
        }
        weightsString += "]";
        return String.Format("{0}-{1}({2}) -> {3}", name, weightsString, bias, value);
    }

}

/// <summary>
/// Class for neural network.
/// </summary>
[DataContract(Name = "NeuralNetwork")]
public class NeuralNetwork
{

    [DataMember]
    public int inputCount;
    [DataMember]
    public int outputCount;
    [DataMember]
    private int hiddenLayers;
    [DataMember]
    private int neuronsInLayer;
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
    [DataMember]
    private List<Neuron> inputNeurons = new List<Neuron>();
    [DataMember]
    public List<Neuron> hiddenNeurons = new List<Neuron>();
    [DataMember]
    public List<Neuron> outputNeurons = new List<Neuron>();

    /// <summary>
    /// Parameterless contructor, needed for loading from file
    /// </summary>
    public NeuralNetwork() { }

    /// <param name="inputCount">Number of input neurons.</param>
    /// <param name="outputCount">Number of output neurons.</param>
    /// <param name="hiddenLayers">Number of hidden layers.</param>
    /// <param name="neuronsInLayer">Numbers of neurons in each hidden layer.</param>
    public NeuralNetwork(int inputCount, int outputCount, int hiddenLayers, int neuronsInLayer)
    {

        //assigning parameters
        this.inputCount = inputCount;
        this.outputCount = outputCount;
        this.hiddenLayers = hiddenLayers;
        this.neuronsInLayer = neuronsInLayer;

        //assigning id
        id = networkIdCounter;
        networkIdCounter++;

        //adding input neurons
        for (int i = 0; i < inputCount; i++)
        {
            AddInputNeuron("i" + i);
        }

        //adding hidden neurons and connecting them between each other and input neurons
        List<Neuron> previousLayer = new List<Neuron>();
        List<Neuron> currentLayer = new List<Neuron>();
        for (int layer_i = 0; layer_i < hiddenLayers; layer_i++)
        {

            currentLayer.Clear();

            //creating new layer of neurons
            for (int neuron_i = 0; neuron_i < neuronsInLayer; neuron_i++)
            {
                Neuron newNeuron = AddHiddenNeuron(String.Format("h{0}:{1}", layer_i, neuron_i));
                currentLayer.Add(newNeuron);
            }

            //connecting first hidden layer to input neurons
            if (layer_i == 0)
            {
                foreach (Neuron inputNeuron in inputNeurons)
                {
                    foreach (Neuron hiddenNeuron in hiddenNeurons)
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
        for (int i = 0; i < outputCount; i++)
        {
            AddOutputNeuron("o" + i);
        }

        //connecting output neurons to last layer of hidden neurons
        foreach (Neuron hiddenNeuron in currentLayer)
        {
            foreach (Neuron outputNeuron in outputNeurons)
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
    /// <param name="addWeight">Add weight to second neuron.</param>
    private void Connect(Neuron neuron1, Neuron neuron2, bool addWeight = true)
    {
        if (neuron1.type == NeuronType.OutputNeuron)
        {
            throw new WrongNeuronTypeException("Output neuron cannot have output links");
        }
        if (neuron2.type == NeuronType.InputNeuron)
        {
            throw new WrongNeuronTypeException("Input neuron cannot have input links");
        }
        if (addWeight)
        {
            neuron2.weights.Add(0);
        }
        neuron2.inputLinks.Add(neuron1);
        neuron1.outputLinks.Add(neuron2);
    }

    /// <summary>
    /// Adds new input neuron to the neural network.
    /// </summary>
    /// <param name="name">Name of the neuron.</param>
    /// <returns>Created neuron.</returns>
    private Neuron AddInputNeuron(string name)
    {
        Neuron newNeuron = new Neuron(allNeurons.Count, name, NeuronType.InputNeuron);
        inputNeurons.Add(newNeuron);
        allNeurons.Add(newNeuron);
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
        hiddenNeurons.Add(newNeuron);
        allNeurons.Add(newNeuron);
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
        outputNeurons.Add(newNeuron);
        allNeurons.Add(newNeuron);
        return newNeuron;
    }

    /// <summary>
    /// Calculates value of all neurons in the network and returns values of output neurons.
    /// </summary>
    /// <param name="inputs">Inputs to be fed to the network.</param>
    /// <returns>List of values of output neurons.</returns>
    public List<double> Feedforward(List<double> inputs)
    {
        SetInputs(inputs);
        foreach (Neuron neuron in allNeurons)
        {
            neuron.Feedforward();
        }
        List<double> output = new List<double>();
        foreach (Neuron neuron in outputNeurons)
        {
            output.Add(neuron.value);
        }
        return output;
    }

    /// <summary>
    /// Sets values of input neurons.
    /// </summary>
    /// <param name="inputs">List of inputs. Size should match number of input neurons.</param>
    private void SetInputs(List<double> inputs)
    {
        for (int i = 0; i < inputs.Count; i++)
        {
            inputNeurons[i].value = inputs[i];
        }
    }

    /// <summary>
    /// Mutates weights and biases of hidden and output neurons.
    /// </summary>
    /// <param name="power">How likely small mutations are.</param>
    /// <param name="maxMutation">Maximum amount of mutation.</param>
    public void Mutate(double power, double maxMutation)
    {
        foreach (Neuron neuron in hiddenNeurons)
        {
            neuron.Mutate(power, maxMutation);
        }
        foreach (Neuron neuron in outputNeurons)
        {
            neuron.Mutate(power, maxMutation);
        }
    }

    /// <summary>
    /// Does crossover of two neural networks.
    /// </summary>
    /// <param name="network1">First parent.</param>
    /// <param name="network2">Second parent.</param>
    /// <returns>New neural newtwork which is result of crossover.</returns>
    public static NeuralNetwork Crossover(NeuralNetwork network1, NeuralNetwork network2)
    {

        NeuralNetwork newNetwork = new NeuralNetwork(network1.inputCount, network1.outputCount, network1.hiddenLayers, network1.neuronsInLayer);

        //crossover of hidden neurons
        for (int i = 0; i < network1.hiddenNeurons.Count; i++)
        {
            Neuron.NeuronCrossover(newNetwork.hiddenNeurons[i], network1.hiddenNeurons[i], network2.hiddenNeurons[i]);
        }

        //crossover of output neurons
        for (int i = 0; i < network1.outputNeurons.Count; i++)
        {
            Neuron.NeuronCrossover(newNetwork.outputNeurons[i], network1.outputNeurons[i], network2.outputNeurons[i]);
        }

        //useful for debugging
        newNetwork.parent1Id = network1.id;
        newNetwork.parent2Id = network2.id;

        return newNetwork;

    }

    /// <summary>
    /// Converts neural network to a string.
    /// </summary>
    /// <returns>String representation of the network.</returns>
    public override string ToString()
    {
        string s = String.Format("id: {0}\n", id);
        foreach (Neuron neuron in allNeurons)
        {
            s += String.Format("    {0}\n", neuron.ToString());
        }
        return s;
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
        writer.WriteLine("inputCount " + inputCount);
        writer.WriteLine("outputCount " + outputCount);
        writer.WriteLine("hiddenLayers " + hiddenLayers);
        writer.WriteLine("neuronsInLayer " + neuronsInLayer);
        writer.WriteLine("fitness " + fitness);
        writer.WriteLine("breakthroughCount " + breakthroughCount);

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
            foreach (double weight in neuron.weights)
            {
                writer.WriteLine("    w " + weight);
            }
            writer.WriteLine("    b " + neuron.bias);
        }

        writer.WriteLine("----");

        //saving connections between neurons
        foreach (Neuron neuron in allNeurons)
        {
            writer.WriteLine(neuron.id);
            writer.Write("    ");
            foreach (Neuron linkedNeuron in neuron.inputLinks)
            {
                writer.Write(linkedNeuron.id + " ");
            }
            writer.WriteLine();
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
        loadedNetwork.inputCount = Int32.Parse(reader.ReadLine().Split(' ')[1]);
        loadedNetwork.outputCount = Int32.Parse(reader.ReadLine().Split(' ')[1]);
        loadedNetwork.hiddenLayers = Int32.Parse(reader.ReadLine().Split(' ')[1]);
        loadedNetwork.neuronsInLayer = Int32.Parse(reader.ReadLine().Split(' ')[1]);
        loadedNetwork.fitness = Double.Parse(reader.ReadLine().Split(' ')[1]);
        loadedNetwork.breakthroughCount = Int32.Parse(reader.ReadLine().Split(' ')[1]);

        //loading neurons
        while (true)
        {

            //neuron list ends with ----
            string nextLine = reader.ReadLine();
            if (nextLine == "----")
            {
                break;
            }

            //loading general info
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

            //loading weights and bias
            bool neuronEnded = false;
            while (true)
            {
                string[] weightOrBiasLine = reader.ReadLine().Trim().Split(' ');
                string label = weightOrBiasLine[0];
                double value = Double.Parse(weightOrBiasLine[1]);
                if (label == "w")
                {
                    newNeuron.weights.Add(value);
                }
                else if (label == "b")
                {
                    newNeuron.bias = value;
                    //line with bias is the last line of the neuron
                    neuronEnded = true;
                    break;
                }
            }
            if (neuronEnded)
            {
                continue;
            }

        }

        //loading connections between neurons
        while (true)
        {

            //connection list ends with the end of the file
            string nextLine = reader.ReadLine();
            if (nextLine == null)
            {
                break;
            }

            int neuronId = Int32.Parse(nextLine);
            string connectionsLine = reader.ReadLine().Trim();
            if (connectionsLine == "")
            {
                //input neurons don't have input links
                continue;
            }
            string[] connectionIndices = connectionsLine.Split(' ');
            foreach (string connectionIndexString in connectionIndices)
            {
                int connectionIndex = Int32.Parse(connectionIndexString);
                Neuron neuron1 = loadedNetwork.allNeurons[connectionIndex];
                Neuron neuron2 = loadedNetwork.allNeurons[neuronId];
                loadedNetwork.Connect(neuron1, neuron2, false);
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

}

/// <summary>
/// Some testing.
/// </summary>
class Program
{
    static void Main(string[] args)
    {

        Console.WriteLine("Basic test");
        Console.WriteLine();

        NeuralNetwork network1 = new NeuralNetwork(2, 2, 2, 2);
        network1.hiddenNeurons[0].weights[0] = 1;
        network1.hiddenNeurons[2].weights[0] = 1;
        network1.outputNeurons[0].weights[0] = 1;
        network1.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network1);
        Console.WriteLine();

        NeuralNetwork network2 = new NeuralNetwork(2, 2, 2, 2);
        network2.hiddenNeurons[0].weights[0] = 2;
        network2.hiddenNeurons[2].weights[0] = 2;
        network2.outputNeurons[0].weights[0] = 2;
        network2.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network2);
        Console.WriteLine();

        Console.WriteLine("Crossover and mutation test");
        Console.WriteLine();

        NeuralNetwork network3 = NeuralNetwork.Crossover(network1, network2);
        network3.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network3);
        Console.WriteLine();

        network3.hiddenNeurons[0].weights[0] = -0.083920758234613382;
        network3.hiddenNeurons[0].weights[1] = -0.10348547168394251;
        network3.hiddenNeurons[1].weights[0] = 0.9864023685598281;
        network3.hiddenNeurons[1].weights[1] = 0.15295621398799475;
        network3.hiddenNeurons[2].weights[0] = -0.070737256110288152;
        network3.hiddenNeurons[2].weights[1] = -1.0365333983959844;
        network3.hiddenNeurons[3].weights[0] = 0.46109638325388069;
        network3.hiddenNeurons[3].weights[1] = -0.25149451732354522;
        network3.outputNeurons[0].weights[0] = 0.50648030282771972;
        network3.outputNeurons[0].weights[1] = 0.51670017869582552;
        network3.outputNeurons[1].weights[0] = 0.23281424843657786;
        network3.outputNeurons[1].weights[1] = -0.60550942224139681;
        network3.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network3);
        Console.WriteLine();

        Console.WriteLine("xml serialization test");
        Console.WriteLine();

        network3.Serialize("network.xml");

        NeuralNetwork network4 = NeuralNetwork.Deserialize("network.xml");
        network4.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network4);
        Console.WriteLine();

        Console.WriteLine("file save/load test");
        Console.WriteLine();

        network4.SaveToFile("network.txt");

        NeuralNetwork network5 = NeuralNetwork.LoadFromFile("network.txt");
        network5.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network5);
        Console.WriteLine();

        network5.Serialize("network.xml");

        NeuralNetwork network6 = NeuralNetwork.Deserialize("network.xml");
        network6.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network6);
        Console.WriteLine();

    }
}