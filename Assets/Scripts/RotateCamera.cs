using UnityEngine;

public class RotateCamera : MonoBehaviour
{
    private Vector3 _rotationSpeed = new Vector3(0, 5, 0);

    void Start()
    {
        var t = PlayerPrefs.GetFloat("CameraRotationSpeed", 0f);
        var speed = Remap(t, 0f, 1f, 5f, 50f);
        _rotationSpeed = new Vector3(0f, speed, 0f);
    }

    private float Remap(float f, float x1, float y1, float x2, float y2)
    {
        // Remaps a value f from range [x1, y1] to range [x2, y2]
        return x2 + (f - x1) * (y2 - x2) / (y1 - x1);
    }

    // Update is called once per frame
    void Update()
    {
        // Rotate the object around its local axes
        transform.Rotate(_rotationSpeed * Time.deltaTime);
    }
}