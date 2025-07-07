using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace September.Editor.AssetImporter
{
    [Serializable]
    public struct Release
    {
        [JsonProperty("id")] public int ID { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("tag_name")] public string TagName { get; set; }
        [JsonProperty("published_at")] public string PublishedAt { get; set; }
        [JsonProperty("assets")] public List<Asset> Assets { get; set; }
    }

    [Serializable]
    public struct Asset
    {
        [JsonProperty("id")] public int ID { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("download_url")] public string URL { get; set; }
        [JsonProperty("size")] public int Size { get; set; }
    }
}

// データサンプル
// {
//     [
//         id: 219025835,
//         name: 'Release main',
//         tag_name: 'main',
//         published_at: '2025-05-16T07:52:08Z',
//         assets: 
//             [
//                 {
//                     id: 255181855,
//                     name: 'Unity.zip',
//                     download_url: 'https://github.com/RyuichiroYoshida/SepDriveActions/releases/download/main/Unity.zip',
//                     size: 63003695
//                 },
//             ]
//     ],
// }