using UnityEngine;

public class DontDestroyNetworkManager : MonoBehaviour
{
    void Awake() => DontDestroyOnLoad(gameObject);
}