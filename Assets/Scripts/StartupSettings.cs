using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SFB;

public class StartupSettings : MonoBehaviour
{

    public Text openFileText;

    public static string networkFile = "";

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void SelectNetworkFile()
    {

        //path to the executable
        string executableDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
        string initialDirectory = executableDirectory;

        //if there is no "Networks" folder near the executable, create it if possible
        try
        {
            Directory.CreateDirectory(executableDirectory + "\\Networks");
            initialDirectory += "\\Networks";
        }
        catch (System.UnauthorizedAccessException) { }

        //open file dialog
        string[] fileList = StandaloneFileBrowser.OpenFilePanel("Select network file", initialDirectory, "xml", false);
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

    public void StartSimulation()
    {
        SceneManager.LoadScene("MainScene");
    }

}
