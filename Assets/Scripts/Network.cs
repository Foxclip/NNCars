using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public abstract class _Neuron
{

    protected string name;
    public double value = 0;
    private List<Neuron> outputLinks = new List<Neuron>();
    private int inputCount = 0;

    public _Neuron(string name)
    {
        this.name = name;
    }

    public void AddLink(Neuron neuron)
    {
        neuron.weights.Add(0);
        neuron.inputCount++;
        neuron.inputLinks.Add(this);
        outputLinks.Add(neuron);
    }

    public abstract void Feedforward();

}

public class InputNeuron : _Neuron
{

    public InputNeuron(string name) : base(name)
    {
    }

    public override void Feedforward() { }

    public override string ToString()
    {
        return String.Format("{0}->{1}", name, value);
    }

}

public class Neuron : _Neuron
{

    public List<_Neuron> inputLinks = new List<_Neuron>();
    public List<double> weights = new List<double>();
    private double bias;

    public Neuron(string name, List<double> weights = null, double bias = 0) : base(name)
    {
        if (weights != null)
        {
            this.weights = new List<double>(weights);
        }
        this.bias = bias;
    }

    public override void Feedforward()
    {

        List<double> inputs = new List<double>();
        foreach (_Neuron neuron in inputLinks)
        {
            inputs.Add(neuron.value);
        }
        double total = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            total += inputs[i] * weights[i];
        }
        total += bias;
        double output = Math.Tanh(total);
        value = output;
    }

    public void Mutate(double power, double maxMutation)
    {
        for (int i = 0; i < weights.Count; i++)
        {
            double weightMutationRate = Math.Pow(Utils.Rand(), power) * maxMutation;
            weights[i] += Utils.RandBetween(-weightMutationRate, weightMutationRate);
        }
        double biasMutationRate = Math.Pow(Utils.Rand(), power) * maxMutation;
        bias += Utils.RandBetween(-biasMutationRate, biasMutationRate);
    }

    public static void NeuronCrossover(Neuron neuron, Neuron parent1, Neuron parent2)
    {
        List<double> averagedWeights = Utils.ListsAverage(parent1.weights, parent2.weights);
        double averagedBias = (parent1.bias + parent2.bias) / 2.0;
        neuron.weights = averagedWeights;
        neuron.bias = averagedBias;
    }

    public override string ToString()
    {
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
        return String.Format("{0}-{1}({2})->{3}", name, weightsString, bias, value);
    }

}

public class NeuralNetwork
{

    private int hiddenLayers;
    private int neuronsInLayer;
    private static int networkIdCounter = 0;
    public int id = 0;
    public int parent1Id = -1;
    public int parent2Id = -1;
    public double fitness = 0;
    public List<_Neuron> neurons = new List<_Neuron>();
    private List<InputNeuron> inputNeurons = new List<InputNeuron>();
    public List<Neuron> hiddenNeurons = new List<Neuron>();
    public List<Neuron> outputNeurons = new List<Neuron>();

    public NeuralNetwork(int hiddenLayers, int neuronsInLayer)
    {

        this.hiddenLayers = hiddenLayers;
        this.neuronsInLayer = neuronsInLayer;

        id = networkIdCounter;
        networkIdCounter++;

        InputNeuron input1 = new InputNeuron("left");
        InputNeuron input2 = new InputNeuron("center");
        InputNeuron input3 = new InputNeuron("right");
        InputNeuron input4 = new InputNeuron("nextCheckpoint");
        AddInputNeuron(input1);
        AddInputNeuron(input2);
        AddInputNeuron(input3);
        AddInputNeuron(input4);

        List<_Neuron> previousLayer = new List<_Neuron>();
        List<_Neuron> currentLayer = new List<_Neuron>();
        for (int layer_i = 0; layer_i < hiddenLayers; layer_i++)
        {
            currentLayer.Clear();
            for (int neuron_i = 0; neuron_i < neuronsInLayer; neuron_i++)
            {
                Neuron newNeuron = new Neuron(String.Format("h{0}:{1}", layer_i, neuron_i));
                AddHiddenNeuron(newNeuron);
                currentLayer.Add(newNeuron);
            }
            if (layer_i == 0)
            {
                foreach (InputNeuron inputNeuron in inputNeurons)
                {
                    foreach (Neuron hiddenNeuron in hiddenNeurons)
                    {
                        Connect(inputNeuron, hiddenNeuron);
                    }
                }
            }
            if (layer_i > 0)
            {
                foreach (_Neuron previousLayerNeuron in previousLayer)
                {
                    foreach (Neuron currentLayerNeuron in currentLayer)
                    {
                        Connect(previousLayerNeuron, currentLayerNeuron);
                    }
                }
            }
            previousLayer = new List<_Neuron>(currentLayer);
        }

        Neuron output1 = new Neuron("movement");
        Neuron output2 = new Neuron("steering");
        AddOutputNeuron(output1);
        AddOutputNeuron(output2);
        foreach (Neuron hiddenNeuron in currentLayer)
        {
            foreach (Neuron outputNeuron in outputNeurons)
            {
                Connect(hiddenNeuron, outputNeuron);
            }
        }


    }

    private void Connect(_Neuron neuron1, Neuron neuron2)
    {
        neuron1.AddLink(neuron2);
    }

    private void AddInputNeuron(InputNeuron neuron)
    {
        inputNeurons.Add(neuron);
        neurons.Add(neuron);
    }

    private void AddHiddenNeuron(Neuron neuron)
    {
        hiddenNeurons.Add(neuron);
        neurons.Add(neuron);
    }

    private void AddOutputNeuron(Neuron neuron)
    {
        outputNeurons.Add(neuron);
        neurons.Add(neuron);
    }

    public List<double> Feedforward(List<double> inputs)
    {
        SetInputs(inputs);
        foreach (_Neuron neuron in neurons)
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

    private void SetInputs(List<double> inputs)
    {
        for (int i = 0; i < inputs.Count; i++)
        {
            inputNeurons[i].value = inputs[i];
        }
    }

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

    public static NeuralNetwork Crossover(NeuralNetwork network1, NeuralNetwork network2)
    {
        NeuralNetwork newNetwork = new NeuralNetwork(network1.hiddenLayers, network1.neuronsInLayer);
        for (int i = 0; i < network1.hiddenNeurons.Count; i++)
        {
            Neuron.NeuronCrossover(newNetwork.hiddenNeurons[i], network1.hiddenNeurons[i], network2.hiddenNeurons[i]);
        }
        for (int i = 0; i < network1.outputNeurons.Count; i++)
        {
            Neuron.NeuronCrossover(newNetwork.outputNeurons[i], network1.outputNeurons[i], network2.outputNeurons[i]);
        }
        newNetwork.parent1Id = network1.id;
        newNetwork.parent2Id = network2.id;
        return newNetwork;
    }

    public override string ToString()
    {
        string s = String.Format("id: {0}", id);
        foreach (_Neuron neuron in neurons)
        {
            s += String.Format("    {0}", neuron.ToString());
        }
        return s;
    }

}

public class Utils
{

    private static Random random = new Random();

    public static double Rand()
    {
        return random.NextDouble();
    }

    public static double RandBetween(double a, double b)
    {
        return random.NextDouble() * (b - a) + a;
    }

    public static List<double> ListsAverage(List<double> list1, List<double> list2)
    {
        List<double> avgList = new List<double>();
        for (int i = 0; i < list1.Count; i++)
        {
            avgList.Add((list1[i] + list2[i]) / 2.0);
        }
        return avgList;
    }

}

class Program
{
    static void Main(string[] args)
    {

        NeuralNetwork network1 = new NeuralNetwork(2, 2);
        network1.hiddenNeurons[0].weights[0] = 1;
        network1.hiddenNeurons[2].weights[0] = 1;
        network1.outputNeurons[0].weights[0] = 1;
        network1.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network1);
        Console.WriteLine();

        NeuralNetwork network2 = new NeuralNetwork(2, 2);
        network2.hiddenNeurons[0].weights[0] = 2;
        network2.hiddenNeurons[2].weights[0] = 2;
        network2.outputNeurons[0].weights[0] = 2;
        network2.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network2);
        Console.WriteLine();

        NeuralNetwork network3 = NeuralNetwork.Crossover(network1, network2);
        network3.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network3);
        Console.WriteLine();

        network3.Mutate(10, 1000);
        network3.Feedforward(new List<double> { 2, 3 });
        Console.WriteLine(network3);
        Console.WriteLine();


    }
}
