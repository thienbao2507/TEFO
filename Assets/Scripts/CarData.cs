using UnityEngine;

[CreateAssetMenu(fileName = "NewCarData", menuName = "Game/Car Data")]
public class CarData : ScriptableObject
{
    public string carName;
    public float acceleration = 12f;
    public float maxSpeed = 8f;
    public float turnSpeed = 180f;
    public float driftFactor = 0.92f;
}