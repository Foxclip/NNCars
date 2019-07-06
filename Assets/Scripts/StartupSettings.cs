using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SFB;

public class StartupSettings : MonoBehaviour
{

    public Text OpenFileText;

    public static string networkFile = "";

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void SelectNetworkFile()
    {
        string[] fileList = StandaloneFileBrowser.OpenFilePanel("Select network file", Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "xml", false);
        if(fileList.Length > 0)
        {
            networkFile = fileList[0];
            OpenFileText.text = Path.GetFileName(networkFile);
        }
    }

    public void ClearNetworkFile()
    {
        networkFile = "";
        OpenFileText.text = "<none>";
    }

    public void StartSimulation()
    {
        SceneManager.LoadScene("MainScene");
    }

}
