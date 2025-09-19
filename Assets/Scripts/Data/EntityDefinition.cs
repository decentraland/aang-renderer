using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Runtime.Wearables;
using Services;
using UnityEngine;
using Utils;

namespace Data
{
    public class EntityDefinition
    {
        public readonly string URN;
        public readonly string Category;
        public readonly string Thumbnail;
        public readonly EntityType Type;
        public readonly EntityFlags Flags;

        [ItemCanBeNull] private readonly Dictionary<BodyShape, Representation> _representations;

        public Representation[] GetAllRepresentations()
        {
            // TODO: Cleanup
            if (_representations[BodyShape.Female] != null && _representations[BodyShape.Male] != null)
            {
                return new Representation[] { _representations[BodyShape.Female],  _representations[BodyShape.Male] };
            } else if (_representations[BodyShape.Male] != null)
            {
                return new Representation[] { _representations[BodyShape.Male] };
            } else if (_representations[BodyShape.Female] != null)
            {
                return new Representation[] { _representations[BodyShape.Female] };
            }
            else
            {
                return new Representation[] { };
            }
        }

        public bool HasRepresentation(BodyShape bodyShape)
        {
            return _representations[bodyShape] != null;
        }

        public Representation this[BodyShape shape] => _representations[shape] ?? throw new InvalidOperationException(
            $"Missing {shape} representation for {URN}");

        private EntityDefinition(string urn, string category, string thumbnail, EntityType type, EntityFlags flags, Dictionary<BodyShape, Representation> representations)
        {
            URN = urn;
            Category = category;
            Thumbnail = thumbnail;
            Type = type;
            _representations = representations;
            Flags = flags;
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

                if (entityRepresentation == null)
                    return null;

                var repContents = entityRepresentation.contents ?? Array.Empty<string>();
                var repContentsSet = new HashSet<string>(repContents, StringComparer.OrdinalIgnoreCase);

                var entityFilesSet = new HashSet<string>(
                    entity.content.Select(c => c.file),
                    StringComparer.OrdinalIgnoreCase
                );

                var main = entityRepresentation.mainFile;
                var hasMain = !string.IsNullOrWhiteSpace(main);
                var mainInRepContents = hasMain && repContentsSet.Contains(main);
                var mainInEntityContent = hasMain && entityFilesSet.Contains(main);

                if (!(hasMain && mainInRepContents && mainInEntityContent))
                {
                    Debug.Log($"[BodyShapeSanitizer] Dropping rep for bodyShape={bodyShape}, mainFile='{main}' (not valid)");
                    return null;
                }

                var filesDict = entity.content
                    .Where(c => repContentsSet.Contains(c.file))
                    .ToDictionary(
                        c => c.file,
                        c => c.url ?? string.Format(APIService.APICatalyst, c.hash),
                        StringComparer.OrdinalIgnoreCase
                    );

                var representation = new Representation(
                    filesDict,
                    entityRepresentation.mainFile,
                    entityRepresentation.overrideHides is { Length: > 0 }
                        ? entityRepresentation.overrideHides
                        : data.hides.Union(entityRepresentation.overrideReplaces is { Length: > 0 }
                            ? entityRepresentation.overrideReplaces
                            : data.replaces).Distinct().ToArray(),
                    data.removesDefaultHiding ?? Array.Empty<string>()
                );

                return representation;
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
                WearableCategories.FACIAL_FEATURES.Contains(category) ? EntityType.FacialFeature : EntityType.Wearable;
            var flags = entity.IsEmote && entity.metadata.emoteDataADR74.loop ? EntityFlags.Looping : EntityFlags.None;

            var representations = new Dictionary<BodyShape, Representation>
            {
                [BodyShape.Male] = Representation.ForBodyShape(WearablesConstants.BODY_SHAPE_MALE, entity),
                [BodyShape.Female] = Representation.ForBodyShape(WearablesConstants.BODY_SHAPE_FEMALE, entity)
            };

            return new EntityDefinition(urn, category, thumbnail, type, flags, representations);
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
                    : WearableCategories.FACIAL_FEATURES.Contains(data.category)
                        ? EntityType.FacialFeature
                        : EntityType.Wearable;
            var flags = isEmote && metadata.emoteDataADR74.loop ? EntityFlags.Looping : EntityFlags.None;

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
                flags,
                representations
            );
        }

        public static EntityDefinition FromEmbeddedEmote(string emote, bool loop)
        {
            return new EntityDefinition(
                $"embedded:{emote}",
                "emote",
                null,
                EntityType.Emote,
                loop ? EntityFlags.Looping : EntityFlags.None,
                new Dictionary<BodyShape, Representation>
                {
                    [BodyShape.Male] = Representation.ForEmbeddedEmote(emote),
                    [BodyShape.Female] = Representation.ForEmbeddedEmote(emote)
                }
            );
        }
    }

    [Flags]
    public enum EntityFlags : byte
    {
        None = 0,
        Looping = 1 << 0,
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