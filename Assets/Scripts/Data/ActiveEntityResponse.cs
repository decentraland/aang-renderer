using System;

namespace Data
{
    [Serializable]
    public class ActiveEntity
    {
        public string id;
        public string[] pointers;
        public string type;
        public Content[] content;
        public Metadata metadata;

        public bool IsEmote => type == "emote";

        [Serializable]
        public class Content
        {
            public string file;
            public string hash;

            public string url; // Used by Base64 entities only
        }

        [Serializable]
        public class Metadata
        {
            public string id;
            public string name;
            public string thumbnail;
            public Data data;
            public EmoteData emoteDataADR74;

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
                public string[] contents;
                public string[] overrideHides;
                public string[] overrideReplaces;
            }
        }
    }
}