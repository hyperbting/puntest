using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEngine;

public static class UniqueRoomPropertyHelper
{
    public static bool GetUniqueRoomProperty<T>(string key, out T current)
    {
        current = default(T);
        // Ensure the client is in a room
        if (!PhotonNetwork.InRoom)
            return false;

        // Attempt to retrieve the property value
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out var val)) 
            return false;
        
        try
        {
            current = (T)val;
            return true;
        }
        catch (InvalidCastException)
        {
            //Debug.LogWarning($"Failed to cast property '{key}' to type {typeof(T)}.");
        }

        return false;
    }
    
    #region Task version
    public static async Task<(bool success, T value)> GetUniqueRoomProperty<T>(string key, T defaultValue)
    {
        // Ensure the client is in a room
        if (!PhotonNetwork.InRoom)
            return (false, default(T));

        // Attempt to retrieve the property value
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out var val))
        {
            return (true, (T)val);
        }

        var success = PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable{{key, defaultValue}});
        if (!success)
            return (false, defaultValue);
        
        // Poll for the expected value in a loop
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalSeconds < 30)
        {
            // Check if the property matches the expected new value
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out var currentObj) && EqualityComparer<T>.Default.Equals((T)currentObj, defaultValue))
            {
                return (true, (T)currentObj);
            }

            // Wait for a short duration before checking again
            await Task.Delay(100);
        }

        return (false, default(T));
    }
    public static async Task<bool> SetUniqueRoomPropertyCAS<T>(string key, T newValue, T previousValue)
    {
        // Ensure no duplicate coroutine for the same key
        if (!_activeKeys.TryAdd(key, true)) // Add returns false if the key already exists
        {
            return false;
        }
        
        // Ensure only the Master Client can set properties
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return false;

        try
        {
            var success = PhotonNetwork.CurrentRoom.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable { { key, newValue } },
                new ExitGames.Client.Photon.Hashtable { { key, previousValue } }
            );

            if (!success)
                return false;

            // Poll for the expected value in a loop
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalSeconds < 30)
            {
                // Check if the property matches the expected new value
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out var currentValue) &&
                    EqualityComparer<T>.Default.Equals((T)currentValue, newValue))
                {
                    return true;
                }

                // Wait for a short duration before checking again
                await Task.Delay(100);
            }
        }
        finally
        {
            _activeKeys.TryRemove(key, out _);;
        }
        
        // Timed out
        return false;
    }
    #endregion

    public static IEnumerator GetUniqueRoomPropertyAsMCCoroutine<T>(string key, T defaultValue, Action<T> success, Action fail)
    {
        yield return new WaitForSeconds(3); // unnecessary delay, but fun for test
        // Ensure the client is in a room
        if (!PhotonNetwork.InRoom)
        {
            fail?.Invoke();
            yield break;
        }

        // Attempt to retrieve the property value
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out var val))
        {
            success?.Invoke((T)val);
            yield break;
        }
        
        if (!PhotonNetwork.IsMasterClient)
        {
            fail?.Invoke();
            yield break;
        }
        
        var res = PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable{{key, defaultValue}});
        if (!res)
        {
            fail?.Invoke();
            yield break;
        }
        
        // Poll for value set in a loop
        for (var i = 0; i < 30; i++)
        {
            // Check if the property matches the expected new value
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out var currentValue))
            {
                success?.Invoke((T)currentValue);
                yield break;
            }

            // Wait for a short duration before checking again
            yield return new WaitForSeconds(1);
        }

        // Timed out
        fail?.Invoke();
    }

    // Tracks keys with active coroutines
    private static readonly ConcurrentDictionary<string,bool> _activeKeys = new ();
    public static IEnumerator SetUniqueRoomPropertyCASCoroutine<T>(string key, T newValue, T previousValue, Action success, Action fail)
    {
        // Ensure no duplicate coroutine for the same key
        if (!_activeKeys.TryAdd(key, true)) // Add returns false if the key already exists
        {
            fail?.Invoke();
            yield break;
        }
        
        try
        {
            // Ensure only the Master Client can set properties
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                fail?.Invoke();
                yield break;
            }

            var res = PhotonNetwork.CurrentRoom.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable { { key, newValue } },
                new ExitGames.Client.Photon.Hashtable { { key, previousValue } }
            );

            if (!res)
            {
                fail?.Invoke();
                yield break;
            }
            
            // Poll for the expected value in a loop
            for (var i = 0; i < 30; i++)
            {
                // Check if the property matches the expected new value
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out var currentValue) && EqualityComparer<T>.Default.Equals((T)currentValue, newValue))
                {
                    success?.Invoke();
                    yield break;
                }

                // Wait for a short duration before checking again
                yield return new WaitForSeconds(1);
            }

            // Timed out
            fail?.Invoke();
        }
        finally
        {
            _activeKeys.TryRemove(key, out _);// Ensure cleanup in all scenarios
        }
    }
    
    public static IEnumerator GetNextIntAndIncrementCoroutine(string key, int defaultValue, Action<bool,int> onComplete)
    {
        var currentValue = -1;
        var success = false;
        
        // Attempt to get the current value but failed; set defaultValue+1 and return defaultValue
        if (!GetUniqueRoomProperty(key, out currentValue))
        {
            // Wait for `GetUniqueRoomProperty` with (1 + default value)
            yield return GetUniqueRoomPropertyAsMCCoroutine(
                key, 
                defaultValue+1,
                (val) =>
                {
                    success = true;
                    Debug.LogWarningFormat($"GetUniqueRoomProperty {key} Not yet set in RomProp, will return {defaultValue}, and Next will be {defaultValue+1}");
                },
                () =>
                {
                    currentValue = -1;
                    Debug.LogError("fail to GetUniqueRoomPropertyAsMCCoroutine");
                }
                
            );
            onComplete?.Invoke(success, defaultValue);
            yield break;
        }
        
        // Attempt CAS update
        yield return SetUniqueRoomPropertyCASCoroutine(
            key,
            currentValue + 1, currentValue,
            () =>
            {
                success = true;
            },
            () =>
            {
                currentValue = -1;
            }
        );
        
        Debug.LogWarningFormat($"SetUniqueRoomPropertyCASCoroutine success: {success}");
        onComplete?.Invoke(success, currentValue);
    }
}
