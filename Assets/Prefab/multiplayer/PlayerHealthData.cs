using Unity.Netcode;
using UnityEngine;
using Unity.Collections; // For FixedString for player names if needed

public class PlayerHealthData : NetworkBehaviour // This is on the NetworkPlayer prefab
{
    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();
    public int startHealth = 30;

    public override void OnNetworkSpawn()
    {
        // Subscribe to changes (this will be handled by HealthUIManager)
        CurrentHealth.OnValueChanged += OnHealthChangedCallback;

        if (IsServer) // Only the server initializes authoritative health
        {
            CurrentHealth.Value = startHealth;
            Debug.Log($"[PlayerHealthData] Server: Initialized health for Player {NetworkObject.OwnerClientId} to {CurrentHealth.Value}");
        }
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -= OnHealthChangedCallback;
    }

    // This callback is primarily for HealthUIManager to subscribe to.
    // It's public so HealthUIManager can add/remove its listener.
    public void OnHealthChangedCallback(int oldHealth, int newHealth)
    {
        Debug.Log($"[PlayerHealthData] Player {NetworkObject.OwnerClientId} (Local: {IsOwner}): Health changed to {newHealth}.");
        if (newHealth <= 0)
        {
            Debug.Log($"[PlayerHealthData] Player {NetworkObject.OwnerClientId} has reached 0 health!");
        }
    }

    // --- Server-side methods to change health (authoritative) ---
    public void Server_IncreaseHealth(int amount)
    {
        if (!IsServer) return;
        CurrentHealth.Value += amount;
        Debug.Log($"[PlayerHealthData] Server: Player {NetworkObject.OwnerClientId} health increased by {amount} to {CurrentHealth.Value}");
    }

    public void Server_DecreaseHealth(int amount)
    {
        if (!IsServer) return;
        CurrentHealth.Value = Mathf.Max(CurrentHealth.Value - amount, 0);
        Debug.Log($"[PlayerHealthData] Server: Player {NetworkObject.OwnerClientId} health decreased by {amount} to {CurrentHealth.Value}");
    }

    // --- Client-side RPC to request health change ---
    [ServerRpc]
    public void RequestChangeHealthServerRpc(int amount, bool increase, ServerRpcParams rpcParams = default) // rpcParams is already here
    {
        if (!IsServer) return;
        // FIX: Access SenderClientId from rpcParams.Receive
        Debug.Log($"[PlayerHealthData ServerRpc] Client {rpcParams.Receive.SenderClientId} requested health change ({increase} by {amount}).");

        if (increase) Server_IncreaseHealth(amount);
        else Server_DecreaseHealth(amount);
    }
}