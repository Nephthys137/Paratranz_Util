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
    public static Dictionary<string, JsonObject> CnDic = [];
    public static Dictionary<string, JsonObject> EnDic = [];
    public static Dictionary<string, JsonObject> JpDic = [];
    public static Dictionary<string, JsonObject> KrDic = [];

#if !DEBUG
    private static readonly Logger Logger = new("./Error.txt");

    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (o, e) => { Logger.Log(o + e.ToString()); };
        try
        {
            _localizePath = new DirectoryInfo(File.ReadAllLines("./LLC_GitHubWrokLocalize_Path.txt")[0]).FullName;
            _paratranzWrokPath = new DirectoryInfo("./Localize").FullName;
#else
    public static void Main(string[] args)
    {
            _localizePath = new DirectoryInfo(File.ReadAllLines("./LLC_GitHubWrokLocalize_Path.txt")[0]).FullName;
            _paratranzWrokPath = new DirectoryInfo("./Localize").FullName;
#endif
            _localizePathLength = _localizePath.Length + 3;
            LoadGitHubWroks(new DirectoryInfo(_localizePath + "/KR"), KrDic);
            LoadGitHubWroks(new DirectoryInfo(_localizePath + "/JP"), JpDic);
            LoadGitHubWroks(new DirectoryInfo(_localizePath + "/EN"), EnDic);
            var rawNickNameObj = Json.Parse(File.ReadAllText(_localizePath + "/NickName.json")).AsObject;
            CnDic["/RawNickName.json"] = rawNickNameObj;
            var nickNameObj = Json.Parse(File.ReadAllText(_localizePath + "/CN/NickName.json")).AsObject;
            CnDic["/NickName.json"] = nickNameObj;

            ToParatranzWrok();

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

    public static void ToParatranzWrokNickName()
    {
        var rawNickNames = CnDic["/RawNickName.json"][0].AsArray;
        JsonArray ptNickName = new();
        foreach (var rnnobj in rawNickNames.List.Cast<JsonObject>())
        {
            var nameKey = rnnobj[0].Value;
            var kr2Has = rnnobj.Dict.TryGetValue("nickName", out var krnickName);
            var enhas = rnnobj.Dict.TryGetValue("enname", out var enname);
            var en2Has = rnnobj.Dict.TryGetValue("enNickName", out var ennickName);
            var jphas = rnnobj.Dict.TryGetValue("jpname", out var jpname);
            var jp2Has = rnnobj.Dict.TryGetValue("jpNickName", out var jpnickName);

            JsonObject krnameobj = new()
            {
                Dict =
                {
                    ["key"] = nameKey + "-krname",
                    ["original"] = nameKey,
                    ["context"] = "EN :\n" + (enhas ? enname.Value : string.Empty) + "\nJP :\n" +
                                  (jphas ? jpname.Value : string.Empty)
                }
            };
            ptNickName.Add(krnameobj);
            if (!kr2Has || string.IsNullOrEmpty(krnickName)) continue;
            JsonObject nickNameobj = new()
            {
                Dict =
                {
                    ["key"] = nameKey + "-nickName",
                    ["original"] = krnickName,
                    ["context"] = "EN :\n" + (en2Has ? ennickName.Value : string.Empty) + "\nJP :\n" +
                                  (jp2Has ? jpnickName.Value : string.Empty)
                }
            };
            ptNickName.Add(nickNameobj);
        }

        File.WriteAllText(_paratranzWrokPath + "/NickName.json", ptNickName.ToString(2));
    }

    public static Dictionary<TKey, TElement> ToDictionaryEx<TSource, TKey, TElement>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector)
    {
        var dictionary = new Dictionary<TKey, TElement>();
        foreach (var item in source)
            dictionary[keySelector(item)] = elementSelector(item);
        return dictionary;
    }

    public static void ToParatranzWrokNone(Dictionary<string, JsonObject> NickNames)
    {
        foreach (var (krkey, kr) in KrDic)
        {
            var isStory = krkey.StartsWith("\\StoryData");
            var en = EnDic.GetValueOrDefault(krkey, kr);
            var jp = JpDic.GetValueOrDefault(krkey, kr);
            JsonArray paratranzWrok = new();
            if (kr.Count == 0)
                continue;
            var krobjs = kr[0].AsArray;
            if (krobjs[0].AsObject.Dict.Count == 0)
                continue;
            Dictionary<string, JsonObject> endic = null;
            Dictionary<string, JsonObject> jpdic = null;
            JsonArray enobjs = null;
            JsonArray jpobjs = null;
            if (isStory)
            {
                enobjs = en[0].AsArray;
                jpobjs = jp[0].AsArray;
            }
            else
            {
                try
                {
                    endic = en[0].AsArray.List.ToDictionaryEx(key => key[0].Value, value => value.AsObject);
                    jpdic = jp[0].AsArray.List.ToDictionaryEx(key => key[0].Value, value => value.AsObject);
                }
                catch
                {
                    endic = kr[0].AsArray.List.ToDictionaryEx(key => key[0].Value, value => value.AsObject);
                    jpdic = endic;
                }
            }

            for (var i = 0; i < krobjs.Count; i++)
            {
                var krobj = krobjs[i].AsObject;
                string objectId = krobj[0];
                JsonObject enobj;
                JsonObject jpobj;
                if (krobj.Count < 1)
                    continue;
                if (isStory)
                {
                    if (objectId == "-1")
                        continue;
                    enobj = enobjs[i].AsObject;
                    jpobj = jpobjs[i].AsObject;
                }
                else
                {
                    enobj = endic.GetValueOrDefault(objectId, krobj);
                    jpobj = jpdic.GetValueOrDefault(objectId, krobj);
                }

                foreach (var keyValue in krobj.Dict)
                {
                    if (keyValue.Value.IsNumber) continue;
                    JsonObject paratranzObject = new()
                    {
                        Dict =
                        {
                            ["key"] = objectId + "-" + keyValue.Key
                        }
                    };
                    if (keyValue.Key == "model")
                    {
                        if (NickNames.TryGetValue(keyValue.Value.Value, out var nickName))
                        {
                            paratranzObject.Dict["original"] = nickName[0];
                            paratranzObject.Dict["translation"] = nickName[1];
                            paratranzObject.Dict["context"] =
                                "这是当前说话人物的默认名称,仅供参考\nEN :\n" + nickName[3] + "\nJP :\n" + nickName[2];
                        }
                        else
                        {
                            paratranzObject.Dict["original"] = keyValue.Value.Value;
                            paratranzObject.Dict["context"] =
                                "这是当前说话人物的默认名称,仅供参考\n但是,令人震惊的是,此版本并没有相关翻译,请前往NickName条目查看相关内容";
                        }

                        paratranzWrok.Add(paratranzObject);
                    }
                    else if (keyValue.Key != "id" && keyValue.Key != "usage")
                    {
                        if (keyValue.Value.IsString)
                        {
                            string original = keyValue.Value;
                            if (string.IsNullOrEmpty(original) || "-".Equals(original)) continue;

                            paratranzObject.Dict["original"] = original;
                            paratranzObject.Dict["context"] =
                                $"EN :\n{enobj[keyValue.Key].Value}\nJP :\n{jpobj[keyValue.Key].Value}";
                        }
                        else if (keyValue.Value.IsArray)
                        {
                            var krps = GetJsonPaths(JArray.Parse(keyValue.Value.ToString()));
                            var enps = GetJsonPaths(JArray.Parse(enobj[keyValue.Key].ToString()));
                            var jpps = GetJsonPaths(JArray.Parse(jpobj[keyValue.Key].ToString()));

                            foreach (var paratranzObject1 in krps.Select(item => new JsonObject
                                     {
                                         Dict =
                                         {
                                             ["key"] = objectId + "-" + keyValue.Key + item.Key,
                                             ["original"] = item.Value.ToString(),
                                             ["context"] = $"EN :\n{enps[item.Key]}\nJP :\n{jpps[item.Key]}"
                                         }
                                     }))
                                paratranzWrok.Add(paratranzObject1);
                            continue;
                        }

                        paratranzWrok.Add(paratranzObject);
                    }
                }
            }

            var filePath = _paratranzWrokPath + krkey;
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            File.WriteAllText(filePath, paratranzWrok.ToString(2));
        }
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

    public static void ToParatranzWrok()
    {
        if (Directory.Exists(_paratranzWrokPath))
            Directory.Delete(_paratranzWrokPath, true);
        Directory.CreateDirectory(_paratranzWrokPath);

        ToParatranzWrokNickName();
        ToParatranzWrokNone(CnDic["/NickName.json"][0].AsArray.List
            .ToDictionary(key => key[0].Value, value => value.AsObject));
    }
}