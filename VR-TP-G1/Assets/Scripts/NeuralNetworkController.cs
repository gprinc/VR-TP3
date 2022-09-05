using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace VRTP3 {
public class NeuralNetworkController : MonoBehaviour{

    public List<int> network = new List<int>();
    public Material connectionMaterial;
    public Material neuronMaterial;
	public GameObject selfConnection;

    private enum NetworkType { MLP, AUTOENCODER, KOHONEN };

    private NetworkType network_type = NetworkType.MLP;

    private int[,] kohonen_activations = { { 1, 2,4 }, { 3, 5,5 }, {1, 6, 2} }; // TODO esto deberia ser userInput 
    private int kohonen_input_dimension = 10;  // TODO esto deberia ser userInput 

    public TextAsset jsonFile;
        
    void Start() {   

        JsonData j = LoadJsonData();
        if (Enum.IsDefined(typeof(NetworkType), j.nn_type)) {
            network_type = (NetworkType)Enum.Parse(typeof(NetworkType), j.nn_type);
        }
        foreach (int number in j.layers) {
            network.Add(number);
        }

        switch (network_type) {

            case NetworkType.MLP:
				Debug.Log("MLP");
				this.transform.position = BuildMLP(network);
                break; 
            
            case NetworkType.AUTOENCODER: 
                Debug.Log("AUTOENCODER");
                List<int> autoencoder = new List<int>();
                autoencoder.AddRange(network);
                network.RemoveAt(network.Count - 1); // latent space is not replicated
                network.Reverse();                   // mirror network
                autoencoder.AddRange(network);
				this.transform.localPosition = BuildMLP(autoencoder);
                break; 

            case NetworkType.KOHONEN:
				this.transform.position = BuildKohonen(kohonen_input_dimension, kohonen_activations);
                break; 
        }
    }
    
    private Vector3 BuildMLP(List<int> network)
    {
        int layers_amount = network.Count;
		int tallestLayer = 0;
        for(int layer_index = 0; layer_index < layers_amount; layer_index++){
            GameObject layer = createLayer(layer_index);
			if (layer.transform.childCount > tallestLayer)
				tallestLayer = layer.transform.childCount;
		}
        for(int l=0; l < layers_amount-1; l++)
            addConnections(l, l+1);
		return new Vector3(-layers_amount/2.0f, tallestLayer/2.0f, 0);
    }

    private GameObject createLayer(int layer_index)
    {
        GameObject layer = new GameObject(string.Format("Layer {0}", layer_index));
        layer.transform.parent = transform;
        for (int neuron_index = 0; neuron_index < network[layer_index]; neuron_index++)
        {
            GameObject neuron = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            neuron.name = string.Format("Neuron {0}", neuron_index);
            neuron.transform.parent = layer.transform;
            neuron.transform.localScale = new Vector3(0.2F, 0.2F, 0.2F);
            neuron.GetComponent<MeshRenderer>().material = neuronMaterial;
            neuron.transform.localPosition = new Vector3(layer_index, neuron_index - (network[layer_index]-1)/2.0f, 0);
            generateLabels(neuron,  string.Format("({0};{1})", layer_index, neuron_index));
        }
        return layer;
    }

    private void addConnections(int first_layer, int second_layer) { 
            // Create Connection Parent 
            GameObject layer1 = GameObject.Find("Layer " + first_layer);
            GameObject layer2 = GameObject.Find("Layer " + second_layer);
            GameObject connections = new GameObject(string.Format("Connections {0}-{1}", first_layer, second_layer));
            connections.transform.parent = transform;

            for (int first_neuron_index = 0; first_neuron_index < layer1.transform.childCount; first_neuron_index++) {
                for(int second_neuron_index = 0; second_neuron_index < layer2.transform.childCount; second_neuron_index++)
                {
                    GameObject connection = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    GameObject neuronA = layer1.transform.Find("Neuron " + first_neuron_index).gameObject;
                    GameObject neuronB = layer2.transform.Find("Neuron " + second_neuron_index).gameObject;
                    Vector3 p1 = neuronA.transform.position;
                    Vector3 p2 = neuronB.transform.position;

                    connection.GetComponent<MeshRenderer>().material = connectionMaterial;
                    connection.name = string.Format("Connection {0}.{1}-{2}.{3}", first_layer, first_neuron_index, second_layer, second_neuron_index);
                    connection.transform.parent = connections.transform;
                    connection.transform.localScale = new Vector3(0.02F, Vector3.Distance(p1, p2)/2, 0.02F);
                    connection.transform.position = (p2 + p1) / 2.0F;
                    connection.transform.up = p2-p1;
                } 
            }    
    }

    private JsonData LoadJsonData() {
        JsonData json = JsonUtility.FromJson<JsonData>(jsonFile.text);
        return json;
    }

    private void generateLabels(GameObject neuron, string text){ 
         
            GameObject label = new GameObject();
             
            label.transform.parent = neuron.transform;
            label.name = "Label";

            //Create TextMesh and modify its properties
            TextMesh textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.characterSize = 0.1F;

            //Set postion of the TextMesh same as Neuron
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.transform.position = new Vector3(neuron.transform.position.x, neuron.transform.position.y, neuron.transform.position.z);
    }
    
    private Vector3 BuildKohonen(int neurons_amount, int[,] activations) { 
        int height = activations.GetLength(0);
        int width = activations.GetLength(1);

        // First Layer
        GameObject layer = createLayer(0);

        // Outside-facing plane
        GameObject last_layer = new GameObject(string.Format("Last Layer"));
        last_layer.transform.parent = transform;
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plane.transform.parent = last_layer.transform;
        plane.transform.localScale = new Vector3(height, 0.001f, width);
        plane.transform.localPosition = new Vector3(5, 0, 0);
        plane.transform.localRotation = Quaternion.Euler(0, 0, 90);

        // Final Neurons
        GameObject top_neurons = new GameObject(string.Format("Layer 1"));
        top_neurons.transform.parent = last_layer.transform;
        for (int i = 0; i < width; i++)
            generateKohonenTopLayerColumn(top_neurons, height, i, width, kohonen_activations);
		addKohonenConnections(top_neurons, width, height);
        addConnections(0, 1);
		return new Vector3(0, height, -6);
	}

    private void generateKohonenTopLayerColumn(GameObject layer, int height, int column, int width,  int[,] kohonen_activations)
    {   
        Color[] activation_colors = GetColors();
        for(int neuron_index = 0; neuron_index < height; neuron_index++)
		{
            GameObject neuron = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            neuron.name = string.Format("Neuron {0}", neuron_index+column* height);
            neuron.transform.parent = layer.transform; 
            neuron.transform.localScale = new Vector3(0.2F, 0.2F, 0.2F); 
            neuron.GetComponent<MeshRenderer>().material = neuronMaterial;
            neuron.transform.localPosition = new Vector3(5, neuron_index - (height - 1)/2.0f, column - (width - 1) / 2.0f);

            generateLabels(neuron, string.Format("(1;{0};{1})", column, neuron_index));

            int min_value = 1; 
            int max_value = 6;
            Color color = GetColor( min_value,  max_value, kohonen_activations[column, neuron_index], activation_colors);
            neuron.GetComponent<MeshRenderer>().material.SetColor("_Color", color);
        }
    }

	private void addKohonenConnections(GameObject layer, int width, int height){
		GameObject connections = new GameObject(string.Format("Connections K"));
		for (int col = 0; col < height; col++)
			for (int row = 0; row < width; row++)
				addKohonenConnection(row, col, width, height, layer, connections);
	}


	private void addKohonenConnection(int row, int col, int width, int height, GameObject layer, GameObject connections) {
		int indexA = row + col*height;
		GameObject neuronA = layer.transform.Find("Neuron " + indexA).gameObject;
		connections.transform.parent = transform;

		Vector3 loop_position = neuronA.transform.localPosition + new Vector3(0.1f, 0, 0);
		GameObject loop = Instantiate(selfConnection, loop_position, Quaternion.identity);
		loop.transform.parent = connections.transform;
		loop.name = string.Format("Connection K{0}", indexA);

		for (int dc = -1; dc <= 1; dc++)
		{
			for (int dr = -1; dr <= 0; dr++)
			{
				int indexB = (row+dr) + (col+dc)*width;
				if (indexA!=indexB && (row + dr)>=0 && indexB >= 0 && indexB < layer.transform.childCount && (dr!=0 || dc<=0)){
					GameObject neuronB = layer.transform.Find("Neuron " + indexB).gameObject;
					GameObject connection = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
					Vector3 p1 = neuronA.transform.position;
					Vector3 p2 = neuronB.transform.position;

					connection.GetComponent<MeshRenderer>().material = connectionMaterial;
					connection.name = string.Format("Connection K{0}-K{1}", indexA, indexB);
					connection.transform.parent = connections.transform;
					connection.transform.localScale = new Vector3(0.02F, Vector3.Distance(p1, p2) / 2, 0.02F);
					connection.transform.position = (p2 + p1) / 2.0F;
					connection.transform.up = p2 - p1;
				}
			}
		}
	}

	private Color[] GetColors() { 

        Color[] colors = { 
            new Color(255, 195, 0, 1.0f), 
            new Color(255, 87, 51 , 1.0f),
            new Color(199, 0, 57, 1.0f),
            new Color(144, 12, 63, 1.0f),
            new Color(88, 24, 69, 1.0f),
            new Color(0, 0, 0, 1.0f)
        };

        return colors;
        
    }

    private Color GetColor(int min_value, int max_value, int value, Color[] colors) {  
        int step = max_value / colors.Count();
        int delta = (int) Mathf.Floor((max_value - min_value) / step);
        return colors[delta];
    }
}
}