using UnityEngine;

public class RotateAroundPivot : MonoBehaviour
{
    public float Distance = 5f;
    public float Speed = 1f;

    private Vector3 Pivot;

    void Start()
    {
        Pivot = transform.position;
    }

    void Update()
    {
        var t = Time.time * Speed;
        transform.position = Pivot + (new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * Distance);
    }
}
