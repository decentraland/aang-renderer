// using System;
// using System.Linq;
//
// namespace Data
// {
//     /// <summary>
//     /// Used when decoding base64 and as a response from wearable collections
//     /// </summary>
//     [Serializable]
//     public class RawActiveEntity
//     {
//         public string id;
//         public Data data;
//         public string thumbnail;
//         public Translation[] i18n;
//         public EmoteData emoteDataADR74;
//
//         // JsonConvert creates empty classes so we can't just check for null
//         public bool IsEmote => !string.IsNullOrEmpty(emoteDataADR74?.category);
//
//         [Serializable]
//         public class Data
//         {
//             public string category;
//             public Representation[] representations;
//             public string[] hides;
//             public string[] replaces;
//             public string[] removesDefaultHiding;
//         }
//
//         [Serializable]
//         public class EmoteData
//         {
//             public string category;
//             public Representation[] representations;
//             public bool loop;
//         }
//
//         [Serializable]
//         public class Representation
//         {
//             public string[] bodyShapes;
//             public string mainFile;
//             public Contents[] contents;
//             public string[] overrideHides;
//             public string[] overrideReplaces;
//
//             [Serializable]
//             public class Contents
//             {
//                 public string key;
//                 public string url;
//             }
//         }
//
//         [Serializable]
//         public class Translation
//         {
//             public string code;
//             public string text;
//         }
//
//         public ActiveEntity ToActiveEntity()
//         {
//             return new ActiveEntity
//             {
//                 pointers = new[] { id },
//                 type = IsEmote ? "emote" : "wearable",
//                 content = (IsEmote ? emoteDataADR74.representations : data.representations)
//                     .SelectMany(r => r.contents
//                         .Select(c => new ActiveEntity.Content { file = c.key, url = c.url }))
//                     .GroupBy(c => c.file) // This GroupBy + Select First works the same as Distinct
//                     .Select(g => g.First())
//                     .ToArray(),
//                 metadata = new ActiveEntity.Metadata
//                 {
//                     id = id,
//                     thumbnail = thumbnail,
//                     name = i18n.First(t => t.code == "en").text,
//                     data = IsEmote
//                         ? null
//                         : new ActiveEntity.Metadata.Data
//                         {
//                             category = data.category,
//                             hides = data.hides,
//                             replaces = data.replaces,
//                             removesDefaultHiding = data.removesDefaultHiding,
//                             representations = data.representations.Select(r => new ActiveEntity.Metadata.Representation
//                             {
//                                 bodyShapes = r.bodyShapes,
//                                 mainFile = r.mainFile,
//                                 overrideHides = r.overrideHides,
//                                 overrideReplaces = r.overrideReplaces,
//                                 contents = r.contents.Select(c => c.key).ToArray()
//                             }).ToArray()
//                         },
//                     emoteDataADR74 = !IsEmote
//                         ? null
//                         : new ActiveEntity.Metadata.EmoteData
//                         {
//                             category = emoteDataADR74.category,
//                             loop = emoteDataADR74.loop,
//                             representations = emoteDataADR74.representations.Select(r =>
//                                 new ActiveEntity.Metadata.Representation
//                                 {
//                                     bodyShapes = r.bodyShapes,
//                                     mainFile = r.mainFile,
//                                     contents = r.contents.Select(c => c.key).ToArray()
//                                 }).ToArray()
//                         }
//                 }
//             };
//         }
//     }
// }