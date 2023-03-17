using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static Settings CurrentSettings { get; private set; }

    public static Action OnSettingsUpdated;
    public static Action OnSettingsReset;

    private const string settingsFile = "UserSettings.json";

    private bool saving;


    private async Task WriteFileAsync(string text, string path)
    {
        await File.WriteAllTextAsync(path, text);
    }


#if !UNITY_WEBGL || UNITY_EDITOR
    private IEnumerator SaveSettingsCoroutine()
    {
        saving = true;

        string filePath = Path.Combine(Application.persistentDataPath, settingsFile);
        //Need to use newtonsoft otherwise dictionaries don't serialize
        string json = JsonConvert.SerializeObject(CurrentSettings);

        Task writeTask = WriteFileAsync(json, filePath);
        yield return new WaitUntil(() => writeTask.IsCompleted);

        if(writeTask.Exception != null)
        {
            Debug.Log($"Failed to save settings with error: {writeTask.Exception.Message}, {writeTask.Exception.StackTrace}");
            ErrorHandler.Instance?.DisplayPopup(ErrorType.Error, "Failed to save your settings!");
        }

        saving = false;
    }


    public void SaveSettings()
    {
        if(saving)
        {
            Debug.Log("Trying to save settings when already saving!");
            return;
        }

        StartCoroutine(SaveSettingsCoroutine());
    }


    private void LoadSettings()
    {
        string filePath = Path.Combine(Application.persistentDataPath, settingsFile);
        
        if(!File.Exists(filePath))
        {
            Debug.Log("Settings file doesn't exist. Using defaults.");
            CurrentSettings = Settings.GetDefaultSettings();
            SaveSettings();

            OnSettingsUpdated?.Invoke();
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            CurrentSettings = JsonConvert.DeserializeObject<Settings>(json);
        }
        catch(Exception err)
        {
            ErrorHandler.Instance?.DisplayPopup(ErrorType.Error, "Failed to load settings! Reverting to default.");
            Debug.LogWarning($"Failed to load settings with error: {err.Message}, {err.StackTrace}");

            CurrentSettings = Settings.GetDefaultSettings();
            SaveSettings();
        }

        OnSettingsUpdated?.Invoke();
    }
#endif


    public static bool GetBool(string name)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        //Use player prefs for WebGL
        //Use ints for bools since PlayerPrefs can't store them
        int defaultValue = 0;
        if(Settings.DefaultSettings.Bools.ContainsKey(name))
        {
            defaultValue = Settings.DefaultSettings.Bools[name] ? 1 : 0;
        }

        return PlayerPrefs.GetInt(name, defaultValue) > 0;
#else
        if(CurrentSettings.Bools.ContainsKey(name))
        {
            return CurrentSettings.Bools[name];
        }
        else if(Settings.GetDefaultSettings().Bools.ContainsKey(name))
        {
            return Settings.GetDefaultSettings().Bools[name];
        }
        else return false;
#endif
    }


    public static int GetInt(string name)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        //Use player prefs for WebGL
        int defaultValue = 0;
        if(Settings.DefaultSettings.Ints.ContainsKey(name))
        {
            defaultValue = Settings.DefaultSettings.Ints[name];
        }

        return PlayerPrefs.GetInt(name, defaultValue);
#else
        if(CurrentSettings.Ints.ContainsKey(name))
        {
            return CurrentSettings.Ints[name];
        }
        else if(Settings.GetDefaultSettings().Ints.ContainsKey(name))
        {
            return Settings.GetDefaultSettings().Ints[name];
        }
        else return 0;
#endif
    }


    public static float GetFloat(string name)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        //Use player prefs for WebGL
        float defaultValue = 0;
        if(Settings.DefaultSettings.Floats.ContainsKey(name))
        {
            defaultValue = Settings.DefaultSettings.Floats[name];
        }

        return PlayerPrefs.GetFloat(name, defaultValue);
#else
        if(CurrentSettings.Floats.ContainsKey(name))
        {
            return CurrentSettings.Floats[name];
        }
        else if(Settings.GetDefaultSettings().Floats.ContainsKey(name))
        {
            return Settings.GetDefaultSettings().Floats[name];
        }
        else return 0;
#endif
    }


    public static void SetRule(string name, bool value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.SetInt(name, value ? 1 : 0);
#else
        Dictionary<string, bool> rules = CurrentSettings.Bools;
        if(rules.ContainsKey(name))
        {
            rules[name] = value;
        }
        else
        {
            rules.Add(name, value);
        }
#endif
        OnSettingsUpdated?.Invoke();
    }


    public static void SetRule(string name, int value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.SetInt(name, value);
#else
        Dictionary<string, int> rules = CurrentSettings.Ints;
        if(rules.ContainsKey(name))
        {
            rules[name] = value;
        }
        else
        {
            rules.Add(name, value);
        }
#endif
        OnSettingsUpdated?.Invoke();
    }


    public static void SetRule(string name, float value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.SetFloat(name, value);
#else
        value = (float)Math.Round(value, 3); //Why tf does the compiler read value as a double here?

        Dictionary<string, float> rules = CurrentSettings.Floats;
        if(rules.ContainsKey(name))
        {
            rules[name] = value;
        }
        else
        {
            rules.Add(name, value);
        }
#endif
        OnSettingsUpdated?.Invoke();
    }


    public static void SetDefaults()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.DeleteAll();
#else
        CurrentSettings = Settings.GetDefaultSettings();
#endif
        
        OnSettingsReset?.Invoke();
        OnSettingsUpdated?.Invoke();
    }


    private void Awake()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        //Load settings from json if not running in WebGL
        //Otherwise settings are handled through playerprefs instead
        LoadSettings();
#else
        OnSettingsUpdated?.Invoke();
#endif
    }
}


[Serializable]
public class Settings
{
    public Dictionary<string, bool> Bools;
    public Dictionary<string, int> Ints;
    public Dictionary<string, float> Floats;


    public static readonly Settings DefaultSettings = new Settings
    {
        Bools = new Dictionary<string, bool>
        {
            {"randomhitsoundpitch", false},
            {"spatialhitsounds", false},
            {"simplenotes", false},
            {"moveanimations", true},
            {"rotateanimations", true},
            {"vsync", true},
            {"ssao", true},
            {"dynamicsoundpriority", true}
        },

        Ints = new Dictionary<string, int>
        {
            {"hitsound", 0},
            {"arcdensity", 60},
            {"camerafov", 80},
            {"cameratilt", 0},
            {"framecap", 60},
            {"antialiasing", 0},
            {"cachesize", 3}
        },

        Floats = new Dictionary<string, float>
        {
            {"musicvolume", 0.5f},
            {"hitsoundvolume", 0.5f},
            {"uiscale", 1f},
            {"chainvolume", 0.8f},
            {"wallopacity", 0.5f},
            {"cameraposition", -2},
            {"bloom", 1},
            {"backgroundbloom", 1},
            {"hitsoundbuffer", 0.2f}
        }
    };


    public static Settings GetDefaultSettings()
    {
        //Provides a deep copy of the default settings I hate reference types I hate reference types I hate reference types I hate reference types I hate reference types
        Settings settings = new Settings
        {
            Bools = new Dictionary<string, bool>(),
            Ints = new Dictionary<string, int>(),
            Floats = new Dictionary<string, float>()
        };

        foreach(var key in DefaultSettings.Bools)
        {
            settings.Bools.Add(key.Key, key.Value);
        }

        foreach(var key in DefaultSettings.Ints)
        {
            settings.Ints.Add(key.Key, key.Value);
        }

        foreach(var key in DefaultSettings.Floats)
        {
            settings.Floats.Add(key.Key, key.Value);
        }

        return settings;
    }
}