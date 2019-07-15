using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using UnityEngine;

/// <summary>
/// Catching log errors and showing them in a message.
/// </summary>
public class ErrorMessage : MonoBehaviour
{
    private void OnEnable()
    {
        UnityEngine.Application.logMessageReceived += this.LogCallback;
    }

    // called when there is error
    private void LogCallback(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
        {
            // if it tries to show several errors at once, we show only the first by quitting early
            #if !UNITY_EDITOR
            UnityEngine.Application.Quit();
            MessageBox.Show(condition, "OOPSIE");
            #endif
        }
    }

    private void OnDisable()
    {
        UnityEngine.Application.logMessageReceived -= this.LogCallback;
    }
}
