using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;

public static class EntityService
{
    private static readonly Dictionary<string, EntityDefinition> CACHED_ENTITIES = new();

    public static async Awaitable<EntityDefinition[]> GetEntities(string[] urns)
    {
        var missingEntities = urns.Where(urn => !CACHED_ENTITIES.ContainsKey(urn)).ToArray();

        if (missingEntities.Length > 0)
        {
            var results =
                (await APIService.GetActiveEntities(missingEntities))
                .Select(EntityDefinition.FromActiveEntity).ToList();

            foreach (var ed in results)
            {
                CACHED_ENTITIES.Add(ed.URN, ed);
            }
        }

        return urns.Select(urn => CACHED_ENTITIES[urn]).ToArray();
    }

    public static EntityDefinition GetCachedEntity(string urn) => CACHED_ENTITIES[urn];

    public static EntityDefinition GetBodyEntity(BodyShape bodyShape)
    {
        return bodyShape switch
        {
            BodyShape.Male => CACHED_ENTITIES[WearablesConstants.BODY_SHAPE_MALE.ToLowerInvariant()],
            BodyShape.Female => CACHED_ENTITIES[WearablesConstants.BODY_SHAPE_FEMALE.ToLowerInvariant()],
            _ => throw new ArgumentOutOfRangeException(nameof(bodyShape), bodyShape, null)
        };
    }

    public static void PreloadCachedEntityAssets()
    {
        JSBridge.NativeCalls.PreloadURLs(string.Join(',',
            CACHED_ENTITIES.Values.SelectMany(ed => ed.GetAllRepresentations()).SelectMany(r => r.Files.Values)
                .Distinct()));
    }
}