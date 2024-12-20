using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public partial class EnemySpawnerPhotonSync
{

#region bot related
    //one andOnly
    [SerializeField] private static string BotIDName = "bot_spw_id";
    private static readonly int defaultBotID = 1;
    public static int GetNextID
    {
        get
        {
            // Ensure only the Master Client can generate IDs
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
                return -1;
            
            // Capture current ID and increment safely
            var retrieved = 1; // default 1
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(BotIDName, out var val))
            {
                retrieved = (int)val;
            }
                
            return retrieved;
        }
    }
    
    #region Task version is cleaner for sure...
#if UNITY_EDITOR
    [ContextMenu("Debug GetNextIDAndIncrement - Task version")]
    public async void DebugGetNextIDAndIncrement()
    {
        var res =  await GetNextIDAndIncrement();
        if (res  == -1)
        {
            Debug.Log("Failed to get next ID. Make sure the conditions are met.");
        }
        else
        {
            Debug.LogFormat("Next ID obtained and incremented: {0}", res);
        }
    }
#endif
    private static async System.Threading.Tasks.Task<int> GetNextIDAndIncrement()
    {
        int currentValue;
        var success = UniqueRoomPropertyHelper.GetUniqueRoomProperty(BotIDName, out currentValue);
        if (success)
        {
            Debug.LogWarningFormat($"GetNextIDAndIncrement success : current {currentValue}");
        }
        else
        {
            (success, currentValue) = await UniqueRoomPropertyHelper.GetUniqueRoomProperty(BotIDName, defaultBotID);
            if (!success)
            {
                Debug.LogWarningFormat($"GetNextIDAndIncrement failed withDefault Set");
                return -1;
            }
        }
        
        var res = await UniqueRoomPropertyHelper.SetUniqueRoomPropertyCAS(BotIDName, currentValue+1, currentValue);
        if (!res)
            return -1;
        
        return currentValue;
    }
    #endregion


    // private void FixedUpdate()
    // {
    //     if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
    //         return;
    //
    //     // StartCoroutine(UniqueRoomPropertyHelper.SetUniqueRoomPropertyCASCoroutine(BotIDName,2,1, () => { Debug.LogFormat("success");}, () => { Debug.Log("Failed");}));
    //     var res =  StartCoroutine(GetNextIDAndIncrementCoroutine((success, res) =>
    //     {
    //         if (!success)
    //         {
    //             Debug.Log("Failed to get next ID. Make sure the conditions are met.");
    //         }
    //         else
    //         {
    //             Debug.LogFormat("ID obtained: {0} and incrementing", res);
    //         }
    //     }));
    // }
#if UNITY_EDITOR
    [ContextMenu("Debug GetNextIDAndIncrementCoroutine")]
    public void GetNextIDAndIncrementCoroutine()
    {
        var res =  StartCoroutine(GetNextIDAndIncrementCoroutine((success, res) =>
        {
            if (!success)
            {
                Debug.Log("Failed to get next ID. Make sure the conditions are met.");
            }
            else
            {
                Debug.LogFormat("Next ID obtained and incremented: {0}", res);
            }
        }));
    }
#endif
    public IEnumerator GetNextIDAndIncrementCoroutine(Action<bool,int> onComplete) => UniqueRoomPropertyHelper.GetNextIntAndIncrementCoroutine(BotIDName, 1, onComplete);
    #endregion
}
