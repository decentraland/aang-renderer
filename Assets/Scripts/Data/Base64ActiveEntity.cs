using System;
using System.Linq;

namespace Data
{
    [Serializable]
    public class Base64ActiveEntity
    {
        public string id;
        public Data data;
        public EmoteData emoteDataADR74;

        // JsonConvert creates empty classes so we can't just check for null
        public bool IsEmote => !string.IsNullOrEmpty(emoteDataADR74?.category);

        [Serializable]
        public class Data
        {
            public string category;
            public Representation[] representations;
            public string[] hides;
            public string[] replaces;
            public string[] removesDefaultHiding;
        }

        [Serializable]
        public class EmoteData
        {
            public string category;
            public Representation[] representations;
            public bool loop;
        }

        [Serializable]
        public class Representation
        {
            public string[] bodyShapes;
            public string mainFile;
            public Contents[] contents;
            public string[] overrideHides;
            public string[] overrideReplaces;

            [Serializable]
            public class Contents
            {
                public string key;
                public string url;
            }
        }

        public ActiveEntity ToActiveEntity()
        {
            return new ActiveEntity
            {
                pointers = new[] { id },
                type = IsEmote ? "emote" : "wearable",
                content = (IsEmote ? emoteDataADR74.representations : data.representations)
                    .SelectMany(r => r.contents
                        .Select(c => new ActiveEntity.Content { file = c.key, url = c.url })).ToArray(),
                metadata = new ActiveEntity.Metadata
                {
                    id = id,
                    data = IsEmote
                        ? null
                        : new ActiveEntity.Metadata.Data
                        {
                            category = data.category,
                            hides = data.hides,
                            replaces = data.replaces,
                            removesDefaultHiding = data.removesDefaultHiding,
                            representations = data.representations.Select(r => new ActiveEntity.Metadata.Representation
                            {
                                bodyShapes = r.bodyShapes,
                                mainFile = r.mainFile,
                                overrideHides = r.overrideHides,
                                overrideReplaces = r.overrideReplaces,
                                contents = r.contents.Select(c => c.key).ToArray()
                            }).ToArray()
                        },
                    emoteDataADR74 = !IsEmote
                        ? null
                        : new ActiveEntity.Metadata.EmoteData
                        {
                            category = emoteDataADR74.category,
                            loop = emoteDataADR74.loop,
                            representations = emoteDataADR74.representations.Select(r =>
                                new ActiveEntity.Metadata.Representation
                                {
                                    bodyShapes = r.bodyShapes,
                                    mainFile = r.mainFile,
                                    contents = r.contents.Select(c => c.key).ToArray()
                                }).ToArray()
                        }
                }
            };
        }
    }
}