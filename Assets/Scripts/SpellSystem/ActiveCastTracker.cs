using UnityEngine;
using Fusion;

public class ActiveCastTracker : NetworkBehaviour
{
    const int ACTIVE_CAST_CAPACITY = 100;

    [Networked, Capacity(ACTIVE_CAST_CAPACITY)]
    public NetworkArray<NetworkCastData> ActiveCastsData { get; }

    [SerializeField]
    public int CurrentActiveCasts;

    public void RegisterNetworkedCast(NetworkCastData newCastData)
    {
        if (!HasStateAuthority && !HasInputAuthority) return; //if we a proxy we dont add 

        for (int i = 0; i < ActiveCastsData.Length; i++)
        {
            if (!ActiveCastsData[i].CastID.IsValid)
            {
                ActiveCastsData.Set(i, newCastData);
                return; 
            }
        }

        Debug.LogError($"[{gameObject.name}] ActiveCastTracker array is completely full! Cannot register new cast.");
    }

    public void RemoveNetworkedCast(ActiveCastID castIdToWipe)
    {

        if (!HasStateAuthority && !HasInputAuthority) return; //if we a proxy we dont take 

        for (int i = 0; i < ActiveCastsData.Length; i++)
        {
            if (ActiveCastsData[i].CastID.Equals(castIdToWipe))
            {
                ActiveCastsData.Set(i, default);
                return;
            }
        }
    }

    public NetworkCastData GetCastData(ActiveCastID castIdToFind)
    {
        for (int i = 0; i < ActiveCastsData.Length; i++)
        {
            if (ActiveCastsData[i].CastID.Equals(castIdToFind))
            {
                return ActiveCastsData[i];
            }
        }
        return default;
    }

    public override void Render()
    {
        base.Render();
        int j = 0;
        for (int i = 0; i < ActiveCastsData.Length; i++)
        {
            if (ActiveCastsData[i].CastID.IsValid)
            {
                j++;
            }
        }

        CurrentActiveCasts = j;
    }
}
