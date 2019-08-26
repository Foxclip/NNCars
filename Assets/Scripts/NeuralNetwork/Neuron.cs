using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

/// <summary>
/// Class for neuron in neural network.
/// </summary>
[Serializable]
[DataContract(Name = "Neuron", IsReference = true)]
public class Neuron
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Neuron"/> class.
    /// Parameterless contructor, needed for loading from file.
    /// </summary>
    public Neuron()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Neuron"/> class.
    /// </summary>
    /// <param name="id">Id of the neuron.</param>
    /// <param name="name">Name of the neuron.</param>
    /// <param name="type">Type of the neuron.</param>
    /// <param name="bias">Bias of the neuron.</param>
    public Neuron(int id, string name, NeuronType type, double bias = 0)
    {
        this.Id = id;
        this.Name = name;
        this.Type = type;
        this.Bias = bias;
    }

    /// <summary>
    /// Value of the neuron. Used by other neurons connected to this one.
    /// </summary>
    public double Value { get; set; } = 0;

    /// <summary>
    /// List of last changes in the weigths.
    /// </summary>
    public List<double> WeightDeltas { get; set; } = new List<double>();

    /// <summary>
    /// Last change of the bias.
    /// </summary>
    public double BiasDelta { get; set; } = 0;

    /// <summary>
    /// Id of layer in neural network, is assigned when sorting.
    /// </summary>
    public int LayerId { get; set; } = 0;

    /// <summary>
    /// Id of the neuron.
    /// </summary>
    [DataMember]
    public int Id { get; set; } = 0;

    /// <summary>
    /// Name of the neuron.
    /// </summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>
    /// Type of the neuron.
    /// </summary>
    [DataMember]
    public NeuronType Type { get; set; }

    /// <summary>
    /// Input links of the neuron. They contain link to neuron and corresponding weight.
    /// </summary>
    [DataMember]
    public List<InputLink> InputLinks { get; set; } = new List<InputLink>();

    /// <summary>
    /// Output links of the neuron.
    /// </summary>
    [DataMember]
    public List<Neuron> OutputLinks { get; set; } = new List<Neuron>();

    /// <summary>
    /// Bias of the neuron.
    /// </summary>
    [DataMember]
    public double Bias { get; set; }

    /// <summary>
    /// Calculates value of the neuron.
    /// </summary>
    public void Feedforward()
    {
        // if it is input neuron, value is already set by SetInputs method of NeuralNetwork class
        if (this.Type == NeuronType.InputNeuron)
        {
            return;
        }

        // collecting inputs from connected neurons
        List<double> inputs = new List<double>();
        foreach (Neuron neuron in this.GetIncomingNeurons())
        {
            inputs.Add(neuron.Value);
        }

        // multiplying inputs by weights
        double total = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            total += inputs[i] * this.InputLinks[i].Weight;
        }

        // adding bias
        total += this.Bias;

        // activation function
        double output = Math.Tanh(total);

        // result is stored in the neuron, so other neurons can use it
        this.Value = output;
    }

    /// <summary>
    /// Randomly mutates weights and biases of the neuron.
    /// </summary>
    /// <param name="power">How likely small mutations are.</param>
    /// <param name="maxMutation">Maximum amount of mutation.</param>
    public void Mutate(double power, double maxMutation)
    {
        // input neurons don't have weights and biases, so there is nothing to mutate
        if (this.Type == NeuronType.InputNeuron)
        {
            throw new NeuralNetwork.NeuralNetworkException("Cannot mutate InputNeuron");
        }

        // mutating weights
        for (int i = 0; i < this.InputLinks.Count; i++)
        {
            double weightMutationRate = Math.Pow(Utils.Rand(), power) * maxMutation;
            this.InputLinks[i].Weight += Utils.RandBetween(-weightMutationRate, weightMutationRate);
        }

        // mutating bias
        double biasMutationRate = Math.Pow(Utils.Rand(), power) * maxMutation;
        this.Bias += Utils.RandBetween(-biasMutationRate, biasMutationRate);
    }

    /// <summary>
    /// Pushes weights and bias in the direction of deltas.
    /// </summary>
    /// <param name="factor">Deltas will be multipled by this number.</param>
    public void Push(double factor)
    {
        for (int i = 0; i < this.GetWeights().Count; i++)
        {
            this.InputLinks[i].Weight += this.WeightDeltas[i] * factor;
        }
        this.Bias += this.BiasDelta * factor;
    }

    /// <summary>
    /// Set deltas based on difference between neurons.
    /// </summary>
    /// <param name="anotherNeuron">Another neuron.</param>
    public void Diff(Neuron anotherNeuron)
    {
        this.WeightDeltas.Clear();
        for (int i = 0; i < this.InputLinks.Count; i++)
        {
            this.WeightDeltas.Add(this.InputLinks[i].Weight - anotherNeuron.InputLinks[i].Weight);
        }
        this.BiasDelta = this.Bias - anotherNeuron.Bias;
    }

    /// <summary>
    /// Returns list with all weights of a neuron.
    /// </summary>
    /// <returns>List of weights.</returns>
    public List<double> GetWeights()
    {
        return (from inputLink in this.InputLinks select inputLink.Weight).ToList();
    }

    /// <summary>
    /// Sets weights of a neuron.
    /// </summary>
    /// <param name="newWeights">List of new weights. Must be same size as amount of weights in the neuron.</param>
    public void SetWeights(List<double> newWeights)
    {
        if (newWeights.Count != this.InputLinks.Count)
        {
            throw new NeuralNetwork.NeuralNetworkException(string.Format("Cannot set weights: trying to set {0} weights while neuron {1} has {2}", newWeights.Count, this.Name, this.InputLinks.Count));
        }

        for (int i = 0; i < newWeights.Count; i++)
        {
            this.InputLinks[i].Weight = newWeights[i];
        }
    }

    /// <summary>
    /// Set specific weight of a neuron.
    /// </summary>
    /// <param name="name">Name of neuron tied to the weight.</param>
    /// <param name="weight">New weight.</param>
    public void SetWeight(string name, double weight)
    {
        List<InputLink> matchingInputLinks = this.InputLinks.FindAll((x) => x.Link.Name == name);
        if (matchingInputLinks.Count == 0)
        {
            throw new NeuralNetwork.NeuralNetworkException(string.Format("Input link with name {0} not found in neuron {1}", name, this.Name));
        }
        else if (matchingInputLinks.Count > 1)
        {
            throw new NeuralNetwork.NeuralNetworkException(string.Format("Found multiple input links with name {0} in neuron {1}", name, this.Name));
        }
        else
        {
            matchingInputLinks[0].Weight = weight;
        }
    }

    /// <summary>
    /// Returns list of all neurons which are feeding values to this neuron.
    /// </summary>
    /// <returns>List of incoming neurons.</returns>
    public List<Neuron> GetIncomingNeurons()
    {
        return (from inputLink in this.InputLinks select inputLink.Link).ToList();
    }

    /// <summary>
    /// Converts neuron to a string.
    /// </summary>
    /// <returns>String representation of the neuron.</returns>
    public override string ToString()
    {
        if (this.Type == NeuronType.InputNeuron)
        {
            return string.Format("{0} -> {1}", this.Name, this.Value);
        }
        string weightsString = "[";
        for (int i = 0; i < this.InputLinks.Count; i++)
        {
            weightsString += this.InputLinks[i].Weight.ToString();
            if (i < this.InputLinks.Count - 1)
            {
                weightsString += " ";
            }
        }
        weightsString += "]";
        return string.Format("{0} {1}({2}) -> {3}", this.Name, weightsString, this.Bias, this.Value);
    }

    /// <summary>
    /// Represents input link of a neuron.
    /// Needed because weights are tied to specific neurons.
    /// </summary>
    [Serializable]
    [DataContract(Name = "InputLink")]
    public class InputLink
    {
        [DataMember]
        private Neuron link;
        [DataMember]
        private double weight;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputLink"/> class.
        /// </summary>
        /// <param name="link">Connected neuron.</param>
        /// <param name="weight">Corresponding weight.</param>
        public InputLink(Neuron link, double weight)
        {
            this.Link = link;
            this.Weight = weight;
        }

        /// <summary>
        /// Connected neuron.
        /// </summary>
        public Neuron Link { get => this.link; set => this.link = value; }

        /// <summary>
        /// Weight associated with connected neuron.
        /// </summary>
        public double Weight { get => this.weight; set => this.weight = value; }
    }
}