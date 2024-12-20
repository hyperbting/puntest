using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public partial class EnemySpawnerPhotonSync : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private string EnemySpawnerName = "bot_spw001";

    /// <summary>
    /// this data is exposed to public/RoomProperty
    /// </summary>
    [Header("Debug")][SerializeField] private EnemySpawnerData debugEnemySpawnerData;

    /// <summary>
    /// these data donot have to expose to public/RoomProperty
    /// but everyone have to keep a latest copy of these
    /// </summary>
     [Serializable]public class EnemySpawnedBotData
     {
         public int botId;
         public int botHp;
         public Vector3 botPosition;
         public Quaternion botRotation;
         public Vector3 botScale;
         public string targetId;
     }
    
    private Dictionary<int, EnemySpawnedBotData> generatedBots = new();
    
    /// <summary>
    /// Reads data from the generated bots dictionary and outputs arrays for each property.
    /// </summary>
    public void ReadFromGeneratedBots(out int[] botIds,out int[] botHps, out Vector3[] botPositions, out Quaternion[] botRotations, out Vector3[] botScales, out string[] targetIds)
    {
        lock (generatedBots)
        {
            botIds = generatedBots.Keys.ToArray();
            botHps = generatedBots.Values.Select(bot => bot.botHp).ToArray();
            botPositions = generatedBots.Values.Select(bot => bot.botPosition).ToArray();
            botRotations = generatedBots.Values.Select(bot => bot.botRotation).ToArray();
            botScales = generatedBots.Values.Select(bot => bot.botScale).ToArray();
            targetIds = generatedBots.Values.Select(bot => bot.targetId).ToArray();
        }
    }

    /// <summary>
    /// Writes data into the generated bots dictionary, updating or adding entries as needed. Optionally removes unpresented keys.
    /// </summary>
    public void WriteIntoGeneratedBots(IEnumerable<EnemySpawnedBotData> datas, bool removeUnpresentedKeys=false)
    {
        if (datas == null) return;

        // Create a set to track the IDs being updated or added
        HashSet<int> currentKeys = new();

        lock (generatedBots)
        {
            foreach (var dat in datas)
            {
                if (dat == null) continue;

                // Update or add to the dictionary
                generatedBots[dat.botId] = dat;
                currentKeys.Add(dat.botId);
            }

            if (!removeUnpresentedKeys) 
                return;
            
            // Remove keys not in the current data set if specified
            foreach (var key in generatedBots.Keys.Except(currentKeys).ToList())
            {
                generatedBots.Remove(key);
            }
        }
    }

    #region Photon Callbacks
    private ExitGames.Client.Photon.Hashtable ht = new ExitGames.Client.Photon.Hashtable();
    public override void OnEnable()
    {
        base.OnEnable();

        if (PhotonNetwork.IsMasterClient)
        {
            SyncWithRoomProperty();
        }
    }

    public override void OnJoinedRoom()
    {
        //base.OnJoinedRoom(); // it is empty
        SyncWithRoomProperty();
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        //base.OnRoomPropertiesUpdate(propertiesThatChanged); // it is empty
        if (propertiesThatChanged.ContainsKey(EnemySpawnerName) && TryLoadEnemySpawnerDataFromRoomProperty(propertiesThatChanged[EnemySpawnerName] as string, out var data))
        {
            debugEnemySpawnerData = data;
        }
        
        // if (propertiesThatChanged.ContainsKey(nextBotIDName) && TryLoadFromRoomPropertyNextBotID(out var retrievedData))
        // {
        //     nextBotID = retrievedData;
        // }
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) 
    {
        // Declare arrays to hold serialized data
        int[] botIds;
        int[] botHps;
        Vector3[] botPositions;
        Quaternion[] botRotations;
        Vector3[] botScales;
        string[] targetIds;
        
        if (stream.IsWriting)
        {
            ReadFromGeneratedBots(out botIds, out botHps, out botPositions, out botRotations,out botScales,out targetIds);
            
            stream.SendNext(botIds);
            stream.SendNext(botHps);
            stream.SendNext(botPositions);
            stream.SendNext(botRotations);
            stream.SendNext(botScales);
            stream.SendNext(targetIds);
        } 
        else if (stream.IsReading)
        {
            botIds = ((int[])stream.ReceiveNext());
            botHps = ((int[])stream.ReceiveNext());
            botPositions = ((Vector3[])stream.ReceiveNext());
            botRotations = ((Quaternion[])stream.ReceiveNext());
            botScales = ((Vector3[])stream.ReceiveNext());
            targetIds = ((string[])stream.ReceiveNext());
            
            // Remove bots not in the received botIds
            foreach (var id in generatedBots.Keys.Except(botIds).ToList())
            {
                generatedBots.Remove(id);
            }
            
            for (var i=0;i < botIds.Length;i++)
            {
                var ke = botIds[i];
                if (!generatedBots.ContainsKey(ke))
                {
                    generatedBots[ke] = new EnemySpawnedBotData
                    {
                        botId = ke,
                    };
                }
                
                generatedBots[ke].botHp = botHps[i];
                generatedBots[ke].botPosition = botPositions[i];
                generatedBots[ke].botRotation = botRotations[i];
                generatedBots[ke].botScale = botScales[i];
                generatedBots[ke].targetId = targetIds[i];
            }
        }
    }
    #endregion

    private void SyncWithRoomProperty(EnemySpawnerData data = null)
    {
        if (!PhotonNetwork.InRoom) 
            return;
        
        if (PhotonNetwork.IsMasterClient)
        {
            if (data is not null)
                debugEnemySpawnerData = data;
            else
                debugEnemySpawnerData = new EnemySpawnerData();
            
            ht.Clear();
            ht[EnemySpawnerName] = debugEnemySpawnerData.ToString();
            
            //MC set Data into RoomProperties
            _ = PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
        }

        // no MC, load Data from RoomProperties
        if (TryLoadEnemySpawnerDataFromRoomProperty(out var retrievedData))
        {
            debugEnemySpawnerData = retrievedData;
        }
    }

    private bool TryLoadEnemySpawnerDataFromRoomProperty(string jsonString,out EnemySpawnerData data)
    {
        try
        {
            data = EnemySpawnerData.FromString(jsonString);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogFormat($"Fail to Parse CustomProperties[{EnemySpawnerName}]: {e}");
        }

        data = null;
        return false;
    }
    
    private bool TryLoadEnemySpawnerDataFromRoomProperty(out EnemySpawnerData data)
    {
        // no MC, load Data from RoomProperties
        var enemySpawnerDataString = PhotonNetwork.CurrentRoom.CustomProperties[EnemySpawnerName] as string;
        return TryLoadEnemySpawnerDataFromRoomProperty(enemySpawnerDataString, out data);
    }

    public void StartInSecond(int milliseconds)
    {
        debugEnemySpawnerData.state = EnemySpawnerData.SpawnerState.InAction;
        debugEnemySpawnerData.startPhotonTimestamp = PhotonNetwork.ServerTimestamp + milliseconds;

        SyncWithRoomProperty(debugEnemySpawnerData);
    }

    [Serializable]
    public class EnemySpawnerData
    {
        public SpawnerState state = SpawnerState.StandBy;
        public int startPhotonTimestamp = 0;
        //data regarding EnemySpawners
        
        public EnemySpawnerData()
        {
            state = SpawnerState.StandBy;
            startPhotonTimestamp = 0;
        }

        public static EnemySpawnerData FromString(string jsonString)
        {
            return JsonConvert.DeserializeObject<EnemySpawnerData>(jsonString);
        }
        
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public enum SpawnerState
        {
            StandBy = 0,
            InAction = 1
        }
    }
}
