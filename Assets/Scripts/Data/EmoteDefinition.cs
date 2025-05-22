using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Data
{
    public class EmoteDefinition
    {
        public Dictionary<string, string> Files { get; private set; }
        public string MainFile { get; private set; }

        public static EmoteDefinition FromActiveEntity(ActiveEntity entity, string bodyShape)
        {
            var representation =
                entity.metadata.emoteDataADR74.representations.FirstOrDefault(r => r.bodyShapes.Contains(bodyShape));
            if (representation == null)
            {
                Debug.LogError("No representation found for body shape: " + bodyShape);
                representation = entity.metadata.data.representations.First();
            }

            return new EmoteDefinition
            {
                Files = entity.content.ToDictionary(c => c.file, c => c.hash),
                MainFile = representation.mainFile,
            };
        }
    }
}