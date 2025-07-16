using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;

namespace Data
{
    public class EntityDefinition
    {
        public readonly string URN;
        public readonly string Category;
        public readonly string Thumbnail;
        public readonly EntityType Type;

        [ItemCanBeNull] private readonly Dictionary<BodyShape, Representation> _representations;

        public Representation this[BodyShape shape] => _representations[shape] ?? throw new InvalidOperationException(
            $"Missing {shape} representation for {URN}");

        private EntityDefinition(string urn, string category, string thumbnail, EntityType type,
            Dictionary<BodyShape, Representation> representations)
        {
            URN = urn;
            Category = category;
            Thumbnail = thumbnail;
            Type = type;
            _representations = representations;
        }

        public class Representation
        {
            public readonly Dictionary<string, string> Files;
            public readonly string MainFile;
            public readonly string[] Hides;
            public readonly string[] RemovesDefaultHiding;

            private Representation(Dictionary<string, string> files, string mainFile, string[] hides,
                string[] removesDefaultHiding)
            {
                Files = files;
                MainFile = mainFile;
                Hides = hides;
                RemovesDefaultHiding = removesDefaultHiding;
            }

            [CanBeNull]
            public static Representation ForBodyShape(string bodyShape, ActiveEntity entity)
            {
                var data = entity.IsEmote ? entity.metadata.emoteDataADR74 : entity.metadata.data;

                var entityRepresentation =
                    data.representations.FirstOrDefault(r => r.bodyShapes.Contains(bodyShape));

                if (entityRepresentation != null)
                {
                    return new Representation(
                        entity.content.Where(c => entityRepresentation.contents.Contains(c.file))
                            .ToDictionary(c => c.file, c => c.url ?? string.Format(APIService.APICatalyst, c.hash)),
                        entityRepresentation.mainFile,

                        // We merge hides and replaces fields because it's the same
                        entityRepresentation.overrideHides is { Length: > 0 }
                            ? entityRepresentation.overrideHides
                            : data.hides.Union(entityRepresentation.overrideReplaces is { Length: > 0 }
                                ? entityRepresentation.overrideReplaces
                                : data.replaces).Distinct().ToArray()
                        ,
                        data.removesDefaultHiding ?? Array.Empty<string>()
                    );
                }

                return null;
            }

            [CanBeNull]
            public static Representation ForBodyShapeRaw(string bodyShape, ActiveEntity.Metadata.Data data)
            {
                throw new NotImplementedException();
            }

            public static Representation ForEmbeddedEmote(string emote)
            {
                return new Representation(
                    new Dictionary<string, string>
                    {
                        ["main"] = Path.Combine(Application.streamingAssetsPath, $"{emote}.glb")
                    },
                    "main", Array.Empty<string>(),
                    Array.Empty<string>()
                );
            }
        }

        public static EntityDefinition FromActiveEntity(ActiveEntity entity)
        {
            var urn = entity.pointers[0];
            var category = entity.IsEmote ? "emote" : entity.metadata.data.category;
            var thumbnail = string.Format(APIService.APICatalyst,
                entity.content.First(c => c.file == entity.metadata.thumbnail).hash);
            var type = entity.IsEmote ? EntityType.Emote :
                urn.Equals(WearablesConstants.BODY_SHAPE_FEMALE, StringComparison.OrdinalIgnoreCase) || urn.Equals(
                    WearablesConstants.BODY_SHAPE_MALE, StringComparison.OrdinalIgnoreCase)  ? EntityType.Body :
                WearablesConstants.FACIAL_FEATURES.Contains(category) ? EntityType.FacialFeature : EntityType.Wearable;

            var representations = new Dictionary<BodyShape, Representation>
            {
                [BodyShape.Male] = Representation.ForBodyShape(WearablesConstants.BODY_SHAPE_MALE, entity),
                [BodyShape.Female] = Representation.ForBodyShape(WearablesConstants.BODY_SHAPE_FEMALE, entity)
            };

            return new EntityDefinition(urn, category, thumbnail, type, representations);
        }

        public static EntityDefinition FromBase64(byte[] b64)
        {
            var base64String = Encoding.UTF8.GetString(b64);
            var metadata = JsonUtility.FromJson<ActiveEntity.Metadata>(base64String);

            // Note: We have to check category because JsonUtility does not support null for custom classes
            if (metadata.data.category == null && metadata.emoteDataADR74.category == null ||
                metadata.data.category != null && metadata.emoteDataADR74.category != null)
            {
                throw new NotSupportedException(
                    "Improper data provided (either data or emoteDataADR74 should be defined, but not both");
            }

            var isEmote = metadata.data.category == null;
            var data = isEmote ? metadata.emoteDataADR74 : metadata.data;
            var type = isEmote
                ? EntityType.Emote
                : metadata.id is WearablesConstants.BODY_SHAPE_FEMALE or WearablesConstants.BODY_SHAPE_MALE
                    ? EntityType.Body
                    : WearablesConstants.FACIAL_FEATURES.Contains(data.category)
                        ? EntityType.FacialFeature
                        : EntityType.Wearable;

            var representations = new Dictionary<BodyShape, Representation>
            {
                [BodyShape.Male] = Representation.ForBodyShapeRaw(WearablesConstants.BODY_SHAPE_MALE, data),
                [BodyShape.Female] = Representation.ForBodyShapeRaw(WearablesConstants.BODY_SHAPE_FEMALE, data)
            };

            return new EntityDefinition(
                metadata.id,
                isEmote ? "emote" : metadata.data.category,
                metadata.thumbnail,
                type,
                representations
            );
        }

        public static EntityDefinition FromEmbeddedEmote(string emote)
        {
            return new EntityDefinition(
                $"embedded:{emote}",
                "emote",
                null,
                EntityType.Emote,
                new Dictionary<BodyShape, Representation>
                {
                    [BodyShape.Male] = Representation.ForEmbeddedEmote(emote),
                    [BodyShape.Female] = Representation.ForEmbeddedEmote(emote)
                }
            );
        }
    }

    public enum EntityType
    {
        Body,
        Wearable,
        FacialFeature,
        Emote
    }

    public enum BodyShape
    {
        Male,
        Female
    }
}