using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SFB;

public class StartupSettings : MonoBehaviour
{

    public static string networksFolderPath = "./Networks";     //path to folder neural networks will be saved to
    public Text openFileText;                                   //text containing filename of neural network

    public static string networkFile = "";                      //neural network will be loaded from this file
    public static int trackIndex = 0;
    public static bool resetFitness = false;                    //whether fitness of loaded neural network will be reset

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void SelectNetworkFile()
    {

        //if there is no "Networks" folder, create it
        Directory.CreateDirectory(networksFolderPath);

        //open file dialog
        string[] fileList = StandaloneFileBrowser.OpenFilePanel("Select network file", networksFolderPath, "txt", false);
        if(fileList.Length > 0)
        {
            networkFile = fileList[0];
            openFileText.text = Path.GetFileName(networkFile);
        }

    }

    public void ClearNetworkFile()
    {
        networkFile = "";
        openFileText.text = "<none>";
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
