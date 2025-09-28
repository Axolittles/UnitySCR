using UnityEngine;

public class SettingsUIController : MonoBehaviour
{
    public void OnAcceptButton()
    {
        // Save something
        Application.Quit();
    }

    public void OnCancelButton()
    {
        Application.Quit();
    }
}
