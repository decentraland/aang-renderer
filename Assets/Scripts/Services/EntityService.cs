using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;
using UnityEngine.Assertions;

namespace Services
{
    public static class EntityService
    {
        private static readonly Dictionary<string, EntityDefinition> CACHED_ENTITIES = new();

        public static async Awaitable<EntityDefinition[]> GetEntities(string[] urns)
        {
            // Sanitize urns
            for (var i = 0; i < urns.Length; i++)
            {
                var originalPointer = urns[i];

                urns[i] = originalPointer.Count(c => c == ':') == 6
                    ? originalPointer.Remove(originalPointer.LastIndexOf(':'))
                    : originalPointer;
            }

            var missingEntities = urns.Where(urn => !CACHED_ENTITIES.ContainsKey(urn)).ToArray();

            if (missingEntities.Length > 0)
            {
                List<EntityDefinition> results;

                try
                {
                    results = (await APIService.GetActiveEntities(missingEntities))
                        .Select(EntityDefinition.FromActiveEntity)
                        .ToList();
                }
                catch (Exception ex)
                {
                    JSBridge.NativeCalls.OnError($"Failed to load entities for URNs: {string.Join(", ", missingEntities)}. {ex.Message}");
                    return urns.Where(urn => CACHED_ENTITIES.ContainsKey(urn))
                        .Select(urn => CACHED_ENTITIES[urn]).ToArray();
                }

                Assert.AreEqual(missingEntities.Length, results.Count, "API did not return all requested entities.");

                if (missingEntities.Length != results.Count)
                {
                    var returnedUrns = results.Select(r => r.URN).ToHashSet();
                    var notReturnedUrns = missingEntities.Where(urn => !returnedUrns.Contains(urn)).ToArray();
                    JSBridge.NativeCalls.OnError($"API did not return entities for URNs: {string.Join(", ", notReturnedUrns)}");
                }

                foreach (var ed in results)
                {
                    CACHED_ENTITIES[ed.URN] = ed;
                }
            }

            return urns.Where(urn => CACHED_ENTITIES.ContainsKey(urn))
                .Select(urn => CACHED_ENTITIES[urn]).ToArray();
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

        public static async Awaitable PreloadBodyEntities()
        {
            await GetEntities(new[]
            {
                WearablesConstants.BODY_SHAPE_MALE.ToLowerInvariant(),
                WearablesConstants.BODY_SHAPE_FEMALE.ToLowerInvariant()
            });
        }

        public static void PreloadCachedEntityAssets()
        {
            JSBridge.NativeCalls.PreloadURLs(string.Join(',',
                CACHED_ENTITIES.Values.SelectMany(ed => ed.GetAllRepresentations()).SelectMany(r => r.Files.Values)
                    .Distinct()));
        }
    }
}