using UnityEngine;

public class CameraPointRegistry : MonoBehaviour
{
    public static CameraPointRegistry Instance { get; private set; }

    [Header("Camera Targets in Scene")]
    public Transform overview;
    public Transform player1;
    public Transform player2;
    public Transform spectator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
