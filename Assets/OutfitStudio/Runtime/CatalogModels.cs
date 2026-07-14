using System;

namespace OutfitStudio
{
    /// <summary>
    /// Query parameters for the marketplace catalog endpoint
    /// (GET marketplace-api.decentraland.{org|zone}/v1/items).
    /// </summary>
    public class CatalogQuery
    {
        /// <summary>"wearable" or "emote".</summary>
        public string Category = "wearable";

        /// <summary>Free text search across names and descriptions.</summary>
        public string Search;

        /// <summary>Wearable slot filter (upper_body, lower_body, feet, hat, ...). Null = any.</summary>
        public string WearableCategory;

        /// <summary>Emote category filter (dance, poses, fun, ...). Null = any.</summary>
        public string EmoteCategory;

        /// <summary>Rarity filter (common ... unique). Null = any.</summary>
        public string Rarity;

        /// <summary>Gender filter (male, female, unisex). Null = any.</summary>
        public string Gender;

        /// <summary>Sort order: newest, name, cheapest.</summary>
        public string SortBy = "newest";

        /// <summary>Specific URNs to look up (used to hydrate slot names/thumbnails).</summary>
        public string[] Urns;

        /// <summary>Filter by published collection contract address (0x...).</summary>
        public string ContractAddress;

        public int First = 24;
        public int Skip;
    }

    [Serializable]
    public class CatalogPage
    {
        public CatalogItem[] data;
        public int total;
    }

    /// <summary>
    /// A marketplace item as returned by /v1/items. Only the fields we consume are declared;
    /// JsonUtility ignores the rest of the payload.
    /// </summary>
    [Serializable]
    public class CatalogItem
    {
        public string id;
        public string name;
        public string thumbnail;
        public string urn;
        public string category; // "wearable" | "emote"
        public string rarity;
        public bool isOnSale;
        public ItemData data;

        /// <summary>The avatar slot this item occupies (wearable category or "emote").</summary>
        public string Slot => category == "emote" ? "emote" : data?.wearable?.category;

        [Serializable]
        public class ItemData
        {
            public WearableData wearable;
            public EmoteData emote;
        }

        [Serializable]
        public class WearableData
        {
            public string[] bodyShapes;
            public string category;
            public bool isSmart;
        }

        [Serializable]
        public class EmoteData
        {
            public string[] bodyShapes;
            public string category;
            public bool loop;
        }
    }
}
