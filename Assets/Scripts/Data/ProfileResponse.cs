using System;
using UnityEngine;

namespace Data
{
    [Serializable]
    public class ProfileResponse
    {
        public Avatar[] avatars;

        [Serializable]
        public class Avatar
        {
            public AvatarData avatar;

            [Serializable]
            public class AvatarData
            {
                public string bodyShape;
                public string[] wearables;
                public ColorData eyes;
                public ColorData hair;
                public ColorData skin;
                
                [Serializable]
                public class ColorData
                {
                    public Color color;
                }
            }
        }
    }
}