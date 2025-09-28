using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUIController : MonoBehaviour
{
    public Slider cameraRotationSpeed;

    public Dictionary<string, float> FloatsToSave = new();
    public Dictionary<string, int> IntsToSave = new();
    public Dictionary<string, string> StringsToSave = new();

    public void Start()
    {
        cameraRotationSpeed.value = PlayerPrefs.GetFloat("CameraRotationSpeed", 0f);
    }

    public void CameraSpeedChanged()
    {
        FloatsToSave["CameraRotationSpeed"] = cameraRotationSpeed.value;
    }

    public void OnAcceptButton()
    {
        foreach (var f in FloatsToSave)
        {
            PlayerPrefs.SetFloat(f.Key, f.Value);
        }
        foreach (var i in IntsToSave)
        {
            PlayerPrefs.SetInt(i.Key, i.Value);
        }
        foreach (var s in StringsToSave)
        {
            PlayerPrefs.SetString(s.Key, s.Value);
        }
        // Reset the Resolution to hide ourselves on start
        Screen.SetResolution(1, 1, FullScreenMode.Windowed);
        Application.Quit();
    }

    public void OnCancelButton()
    {
        // Reset the Resolution to hide ourselves on start
        Screen.SetResolution(1, 1, FullScreenMode.Windowed);
        Application.Quit();
    }
}
