using Unity.Netcode;
using UnityEngine;

public class HandCountTracker : NetworkBehaviour
{
    public static HandCountTracker Local { get; private set; }

    [Header("UI hand container for this client (assign in inspector or at runtime)")]
    [SerializeField] private Transform localHandContainer;

    public NetworkVariable<int> HandCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsOwner) Local = this;

        HandCount.OnValueChanged += (_, __) =>
        {
            if (HandCountHUD.Instance != null)
                HandCountHUD.Instance.Refresh();
        };

        if (HandCountHUD.Instance != null)
            HandCountHUD.Instance.Refresh();

        if (IsOwner)
            Invoke(nameof(ReportLocalHandCount), 0.25f); // small delay so UI is in scene
    }

    public void ReportLocalHandCount()
    {
        if (!IsOwner) return;
        TryResolveHandContainer();
        if (localHandContainer == null) return;

        int count = localHandContainer.GetComponentsInChildren<HandCardUI>(true).Length;
        //for (int i = 0; i < localHandContainer.childCount; i++)
        //{
        //    var child = localHandContainer.GetChild(i);
        //    if (child == null) continue;

        //    if (child.GetComponent<HandCardUI>() != null)
        //        count++;
        //}

        SetHandCountServerRpc(count);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && Local == this)
            Local = null;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetHandCountServerRpc(int newCount, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        HandCount.Value = Mathf.Max(0, newCount);
    }

    private void TryResolveHandContainer()
    {
        if (localHandContainer != null) return;

        // Option A: Tag your hand container as "LocalHandContainer"
        var tagged = GameObject.FindGameObjectWithTag("LocalHandContainer");
        if (tagged != null)
        {
            localHandContainer = tagged.transform;
            return;
        }

        // Option B: Fallback by name (change to your actual object name)
        var byName = GameObject.Find("HandArea"); // or "HandManager/HandArea/Content"
        if (byName != null)
            localHandContainer = byName.transform;
    }

    public void ReportLocalHandCountNextFrame()
    {
        if (!IsOwner) return;
        StartCoroutine(CoReportNextFrame());
    }

    private System.Collections.IEnumerator CoReportNextFrame()
    {
        yield return null; // wait 1 frame for hierarchy/destroy to complete
        ReportLocalHandCount();
    }
}
