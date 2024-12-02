using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SimpleJSON;

namespace LLC_Paratranz_Util;

public static class Program
{
    private static string _localizePath;
    private static string _paratranzWrokPath;
    private static int _localizePathLength;
    private static int _paratranzWrokPathLength;

    private static readonly Logger Logger = new("./Error.txt");
    public static Dictionary<string, JsonObject> CnDic = [];
    public static Dictionary<string, JsonObject> KrDic = [];
    public static Dictionary<string, JsonArray> PtDic = [];

    public static void Main(string[] args)
    {
#if !DEBUG
        AppDomain.CurrentDomain.UnhandledException += (o, e) => { Logger.Log(o + e.ToString()); };
        try
        {
#endif
            _localizePath = new DirectoryInfo(File.ReadAllLines("./LLC_GitHubWrokLocalize_Path.txt")[0]).FullName;
            _localizePathLength = _localizePath.Length + 3;
            _paratranzWrokPath = new DirectoryInfo("./utf8/Localize").FullName;
            _paratranzWrokPathLength = _paratranzWrokPath.Length;
            LoadGitHubWroks(new DirectoryInfo(_localizePath + "/KR"), KrDic);
            var rawNickNameObj = Json.Parse(File.ReadAllText(_localizePath + "/NickName.json")).AsObject;
            CnDic["/RawNickName.json"] = rawNickNameObj;
            var readmeObj = Json.Parse(File.ReadAllText(_localizePath + "/Readme/Readme.json")).AsObject;
            CnDic["/Readme/Readme.json"] = readmeObj;

            LoadParatranzWroks(new DirectoryInfo(_paratranzWrokPath), PtDic);
            ToGitHubWrok();
#if !DEBUG
        }
        catch (Exception ex)
        {
            Logger.Log(ex.ToString());
        }
        Logger.StopLogging();
#endif
    }

    public static void LoadGitHubWroks(DirectoryInfo directory, Dictionary<string, JsonObject> dic)
    {
        foreach (var fileInfo in directory.GetFiles())
        {
            var value = File.ReadAllText(fileInfo.FullName);
            var fileName = fileInfo.DirectoryName!.Remove(0, _localizePathLength) + "/" + fileInfo.Name;
            dic[fileName] = Json.Parse(value).AsObject;
        }

        foreach (var directoryInfo in directory.GetDirectories())
            LoadGitHubWroks(directoryInfo, dic);
    }

    public static void LoadParatranzWroks(DirectoryInfo directory, Dictionary<string, JsonArray> dic)
    {
        foreach (var fileInfo in directory.GetFiles())
        {
            var value = File.ReadAllText(fileInfo.FullName);
            var fileName = fileInfo.DirectoryName!.Remove(0, _paratranzWrokPathLength) + "/" + fileInfo.Name;
            dic[fileName] = Json.Parse(value).AsArray;
        }

        foreach (var directoryInfo in directory.GetDirectories())
            LoadParatranzWroks(directoryInfo, dic);
    }

    public static void ToGitHubWrok()
    {
        if (Directory.Exists(_localizePath + "/CN"))
            Directory.Delete(_localizePath + "/CN", true);
        Directory.CreateDirectory(_localizePath + "/CN");
        KrDic["/NickName.json"] = CnDic["/RawNickName.json"];
        foreach (var ptKvs in PtDic)
        {
            var pt = ptKvs.Value.List.ToDictionary(key => key[0].Value, value => value.AsObject);
            if (!KrDic.TryGetValue(ptKvs.Key, out var kr)) continue;
            var krobjs = kr[0].AsArray;
            for (var i = 0; i < krobjs.Count; i++)
            {
                var krobj = krobjs[i].AsObject;
                string objectId = krobj[0];
                foreach (var keyValue in krobj.Dict.ToArray())
                {
                    if (keyValue.Value.IsNumber || keyValue.Key == "id" || keyValue.Key == "model" ||
                        keyValue.Key == "usage") continue;
                    if (keyValue.Value.IsString)
                    {
                        if (!pt.TryGetValue(objectId + "-" + keyValue.Key, out var ptobj) ||
                            !ptobj.Dict.TryGetValue("translation", out var translation) ||
                            string.IsNullOrEmpty(translation))
                            continue;
                        krobj[keyValue.Key].Value = translation.Value.Replace("\\n", "\n");
                    }
                    else if (keyValue.Value.IsArray)
                    {
                        var token = JArray.Parse(keyValue.Value.ToString());
                        var jps = GetJsonPaths(token);
                        foreach (var item in jps)
                        {
                            if (!pt.TryGetValue(objectId + "-" + keyValue.Key + item.Key, out var ptobj) ||
                                !ptobj.Dict.TryGetValue("translation", out var translation) ||
                                string.IsNullOrEmpty(translation))
                                continue;
                            item.Value.Replace(translation.Value.Replace("\\n", "\n"));
                        }

                        krobj.Dict[keyValue.Key] = Json.Parse(token.ToString());
                    }
                }
            }

            var krjson = kr.ToString();
            var filePath = _localizePath + "/CN" + ptKvs.Key;
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath!);
            File.WriteAllText(filePath, JObject.Parse(krjson).ToString());
        }

        var special = PtDic["/Special.json"].List.ToDictionary(key => key[0].Value, value => value.AsObject);

        var changelog = special["更新记录"]["translation"].Value;
        var parent = new DirectoryInfo(_localizePath).Parent!.FullName;
        File.WriteAllText(parent + "/CHANGELOG.md", changelog.Replace("\\n", "\r\n"));
        var readmeObj = CnDic["/Readme/Readme.json"];
        var noticeList = readmeObj[0].AsArray;
        var ver = changelog.AsSpan(3, changelog.IndexOf("\\n", StringComparison.Ordinal) - 3);
        var llcMod = File.ReadAllText(parent + "/src/LLCMod.cs");
        var startIndex = llcMod.IndexOf("Version = \"", StringComparison.Ordinal);
        var endIndex = llcMod.IndexOf('"', startIndex + 11);
        llcMod = llcMod.Remove(startIndex + 11, endIndex - startIndex - 11).Insert(startIndex + 11, ver.ToString());
        File.WriteAllText(parent + "/src/LLCMod.cs", llcMod);
        var readmeVer = string.Concat("Mod V", ver);
        var notice = noticeList[^2].AsObject;
        if (!notice[6].Value.Equals(readmeVer))
        {
            noticeList[^1] = noticeList[^2].Clone();
            notice[0].AsInt += 1;
            notice[3] = DateTime.Now.ToString("yyyy-MM-dd") + "T00:00:00.000Z";
            notice[6] = readmeVer;
        }

        notice[7] = "{\"list\":[{\"formatKey\":\"Text\",\"formatValue\":\"" + changelog + "\"}]}";
        File.WriteAllText(_localizePath + "/Readme/Readme.json", readmeObj.ToString(2));
        File.WriteAllText(_localizePath + "/Readme/LoadingTexts.md",
            special["Loading"]["translation"].Value.Replace("\\n", "\r\n"));
    }

    public static Dictionary<string, JToken> GetJsonPaths(JToken token, string currentPath = "$")
    {
        var paths = new Dictionary<string, JToken>();
        switch (token)
        {
            case JObject { Count: > 0 } obj:
            {
                foreach (var childPath in from property in obj.Properties()
                         let path = $"{currentPath}.{property.Name}"
                         from childPath in GetJsonPaths(property.Value, path)
                         select childPath)
                    paths[childPath.Key] = childPath.Value;
                break;
            }
            case JArray { Count: > 0 } array:
            {
                for (var i = 0; i < array.Count; i++)
                    foreach (var childPath in GetJsonPaths(array[i], $"{currentPath}[{i}]"))
                        paths[childPath.Key] = childPath.Value;

                break;
            }
            default:
                if (!IsEmpty(token)) paths[currentPath] = token;
                break;
        }

        return paths;
    }

    public static bool IsEmpty(JToken token)
    {
        return token.Type switch
        {
            JTokenType.Null => true,
            JTokenType.String => token.ToString() == string.Empty,
            _ => !token.HasValues
        };
    }
}