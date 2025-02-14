using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Data
{
    public class WearableDefinition
    {
        public string Pointer { get; private set;}
        public string Category { get; private set;}
        
        public Dictionary<string, string> Files { get; private set;}
        public string MainFile { get; private set;}
        
        public string[] Hides { get; private set;}
        public string[] Replaces { get; private set;}

        public static WearableDefinition FromActiveEntity(ActiveEntity entity, string bodyShape)
        {
            var definition = new WearableDefinition
            {
                Pointer = entity.pointers[0],
                Category = entity.metadata.data.category,
                Files = entity.content.ToDictionary(c => c.file, c => c.hash)
            };

            var representation = entity.metadata.data.representations.FirstOrDefault(r => r.bodyShapes.Contains(bodyShape));
            if (representation == null)
            {
                Debug.LogError("No representation found for body shape: " + bodyShape);
                representation = entity.metadata.data.representations.First();
            }

            definition.MainFile = representation.mainFile;
            
            // If the representation has hides we take it from there, otherwise we take it from the entity metadata
            definition.Hides = representation.overrideHides is { Length: > 0 } ? representation.overrideHides : entity.metadata.data.hides;
            
            // If the representation has replaces we take it from there, otherwise we take it from the entity metadata
            definition.Replaces = representation.overrideReplaces is { Length: > 0 } ? representation.overrideReplaces : entity.metadata.data.replaces;
            

            return definition;
        }
    }
}