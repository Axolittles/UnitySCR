using UnityEngine;

public class SettingsUIController : MonoBehaviour
{
    public void OnAcceptButton()
    {
        // Save something
        Screen.SetResolution(1, 1, FullScreenMode.Windowed);
        Application.Quit();
    }

    public void OnCancelButton()
    {
        Screen.SetResolution(1, 1, FullScreenMode.Windowed);
        Application.Quit();
    }
}
