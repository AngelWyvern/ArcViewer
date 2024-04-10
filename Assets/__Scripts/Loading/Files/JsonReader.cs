using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class JsonReader
{
    public static Regex VersionRx = new Regex(@"version""\s*:\s*""(\d\.?)*", RegexOptions.Compiled);


    public static async Task<BeatmapInfo> LoadInfoAsync(string location)
    {
        Debug.Log("Loading text from Info.dat");
        string json = await ReadFileAsync(location);

        if(json == "")
        {
            return null;
        }

        Debug.Log("Parsing Info.dat.");
        BeatmapInfo info = ParseInfoFromJson(json);

        return info;
    }


    public static BeatmapInfo ParseInfoFromJson(string json)
    {
        BeatmapInfo info;

        try
        {
            Match match = VersionRx.Match(json);

            //Get only the version number
            string versionNumber = match.Value.Split('"').Last();
            Debug.Log($"Info.dat is version: {versionNumber}");

            string[] v4Versions = {"4.0.0"};
            string[] v2Versions = {"2.0.0", "2.1.0"};
            
            if(v4Versions.Contains(versionNumber))
            {
                Debug.Log("Parsing Info.dat in V4 format.");
                info = DeserializeObject<BeatmapInfo>(json);
            }
            else if(v2Versions.Contains(versionNumber))
            {
                Debug.Log("Parsing Info.dat in V2 format.");
                BeatmapInfoV2 v2Info = DeserializeObject<BeatmapInfoV2>(json);
                info = v2Info.ConvertToV4();
            }
            else
            {
                Debug.LogWarning("Info.dat has missing or unsupported version.");
                info = ParseInfoFallback(json);
            }
        }
        catch(Exception err)
        {
            Debug.LogWarning($"Unable to parse info from json with error: {err.Message}, {err.StackTrace}.");
            return null;
        }

        return info;
    }


    private static BeatmapInfo ParseInfoFallback(string json)
    {
        Debug.Log("Trying to fallback load Info.dat in V4 format.");
        BeatmapInfo infoV4 = DeserializeObject<BeatmapInfo>(json);

        if(infoV4?.HasFields ?? false)
        {
            Debug.Log("Fallback for Info.dat succeeded in V4.");
            return infoV4;
        }

        Debug.Log("Fallback for Info.dat failed in V4, trying V2.");
        BeatmapInfoV2 infoV2 = DeserializeObject<BeatmapInfoV2>(json);

        if(infoV2?.HasFields ?? false)
        {
            Debug.Log("Fallback for Info.dat succeeded in V2.");
            return infoV2.ConvertToV4();
        }

        Debug.LogWarning("Info.dat is in an unsupported or missing version!");
        return null;
    }


    public static async Task<Difficulty> LoadDifficultyAsync(string directory, DifficultyBeatmap beatmap)
    {
        Difficulty output = new Difficulty
        {
            difficultyRank = MapLoader.DiffValueFromString[beatmap.difficulty],
            noteJumpSpeed = beatmap.noteJumpMovementSpeed,
            spawnOffset = beatmap.noteJumpStartBeatOffset
        };

        string filename = beatmap.beatmapDataFilename;
        Debug.Log($"Loading json from {filename}");

        string location = Path.Combine(directory, filename);
        string json = await ReadFileAsync(location);

        if(string.IsNullOrEmpty(json))
        {
            ErrorHandler.Instance.QueuePopup(ErrorType.Warning, $"Unable to load {filename}!");
            return null;
        }

        Debug.Log($"Parsing {filename}");
        output.beatmapDifficulty = ParseBeatmapFromJson(json, filename);

        return output;
    }


    public static BeatmapDifficulty ParseBeatmapFromJson(string json, string filename = "{UnknownDifficulty}")
    {
        BeatmapDifficulty difficulty;

        try
        {
            string[] v4Versions = {"4.0.0"};
            string[] v3Versions = {"3.0.0", "3.1.0", "3.2.0", "3.3.0"};
            string[] v2Versions = {"2.0.0", "2.2.0", "2.5.0", "2.6.0"};

            Match match = VersionRx.Match(json);

            //Get only the version number
            string versionNumber = match.Value.Split('"').Last();
            Debug.Log($"{filename} is version: {versionNumber}");

            //Search for a matching version and parse the correct map format
            if(v4Versions.Contains(versionNumber))
            {
                Debug.Log($"Parsing {filename} in V3 format.");
                BeatmapDifficultyV4 beatmap = DeserializeObject<BeatmapDifficultyV4>(json);
                difficulty = new BeatmapWrapperV4(beatmap);
            }
            else if(v3Versions.Contains(versionNumber))
            {
                Debug.Log($"Parsing {filename} in V3 format.");
                BeatmapDifficultyV3 beatmap = DeserializeObject<BeatmapDifficultyV3>(json);
                difficulty = new BeatmapWrapperV3(beatmap);
            }
            else if(v2Versions.Contains(versionNumber))
            {
                Debug.Log($"Parsing {filename} in V2 format.");

                BeatmapDifficultyV2 beatmap = DeserializeObject<BeatmapDifficultyV2>(json);
                difficulty = new BeatmapWrapperV3(beatmap.ConvertToV3());
            }
            else
            {
                Debug.LogWarning($"Unable to match map version for {filename}. The map has either a missing or unsupported version.");
                difficulty = ParseBeatmapFallback(json, filename);
            }
        }
        catch(Exception err)
        {
            ErrorHandler.Instance.QueuePopup(ErrorType.Warning, $"Unable to parse {filename}!");
            Debug.LogWarning($"Unable to parse {filename} file with error: {err.Message}, {err.StackTrace}.");
            return BeatmapDifficulty.GetDefault();
        }

        Debug.Log($"Parsed {filename} with {difficulty.Notes.Length} notes, {difficulty.Bombs.Length} bombs, {difficulty.Walls.Length} walls, {difficulty.Arcs.Length} arcs, {difficulty.Chains.Length} chains.");
        return difficulty;
    }


    private static BeatmapDifficulty ParseBeatmapFallback(string json, string filename = "{UnknownDifficulty}")
    {
        Debug.Log($"Trying to fallback load {filename} in V4 format.");
        BeatmapDifficultyV4 v4Diff = DeserializeObject<BeatmapDifficultyV4>(json);

        if(v4Diff?.HasObjects ?? false)
        {
            Debug.Log($"Fallback for {filename} succeeded in V4.");
            return new BeatmapWrapperV4(v4Diff);
        }

        Debug.Log($"Fallback for {filename} failed in V4, trying V3.");
        BeatmapDifficultyV3 v3Diff = DeserializeObject<BeatmapDifficultyV3>(json);

        if(v3Diff?.HasObjects ?? false)
        {
            Debug.Log($"Fallback for {filename} succeeded in V3.");
            return new BeatmapWrapperV3(v3Diff);
        }

        Debug.Log($"Fallback for {filename} failed in V3, trying V2.");
        BeatmapDifficultyV2 v2Diff = DeserializeObject<BeatmapDifficultyV2>(json);

        if(v2Diff?.HasObjects ?? false)
        {
            Debug.Log($"Fallback for {filename} succeeded in V2.");
            return new BeatmapWrapperV3(v2Diff.ConvertToV3());
        }

        ErrorHandler.Instance.QueuePopup(ErrorType.Warning, $"Unable to find difficulty version for {filename}!");
        Debug.LogWarning($"{filename} is in an unsupported or missing version!");
        return BeatmapDifficulty.GetDefault();
    }


    public static async Task<string> ReadFileAsync(string location)
    {
        if(!File.Exists(location))
        {
            Debug.LogWarning("Trying to read a file that doesn't exist!");
            return "";
        }

        try
        {
            string text = await File.ReadAllTextAsync(location);
            return text;
        }
        catch(Exception err)
        {
            Debug.LogWarning($"Unable to read the text from file with error: {err.Message}, {err.StackTrace}.");
            return "";
        }
    }


    public static T DeserializeObject<T>(string json) => JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings
    {
        Error = HandleDeserializationError
    });


    public static void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args)
    {
        Debug.LogWarning($"Error parsing json: {args.ErrorContext.Error.Message}");
        args.ErrorContext.Handled = true;
    }
}