using System;

namespace Data
{
    [Serializable]
    public class ActiveEntity
    {
        public string[] pointers;
        public Content[] content;
        public Metadata metadata;

        [Serializable]
        public class Content
        {
            public string file;
            public string hash;
        }

        [Serializable]
        public class Metadata
        {
            public string id;
            public Data data;

            [Serializable]
            public class Data
            {
                public string category;
                public Representation[] representations;
                public string[] hides;
                public string[] replaces;
                public string[] removesDefaultHiding;

                [Serializable]
                public class Representation
                {
                    public string[] bodyShapes;
                    public string mainFile;
                    public string[] overrideHides;
                    public string[] overrideReplaces;
                }
            }
        }
    }
}