using UnityEngine;

public class Rotate : MonoBehaviour
{
    public Vector3 rotationSpeed = new Vector3(0, 100, 0);

    // Update is called once per frame
    void Update()
    {
        // Rotate the object around its local axes
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}
