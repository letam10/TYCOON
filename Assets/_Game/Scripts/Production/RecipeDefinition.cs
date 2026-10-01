using System;
using System.Collections.Generic;
using UnityEngine;

namespace TYCOON
{
    [Serializable]
    public struct RecipeIngredient
    {
        public ItemDefinition item;
        public int quantity;
        public RecipeIngredient(ItemDefinition item, int quantity) { this.item = item; this.quantity = quantity; }
    }

    [CreateAssetMenu(menuName = "TYCOON/Recipe")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private RecipeIngredient[] ingredients = Array.Empty<RecipeIngredient>();
        [SerializeField] private ItemDefinition output;
        [SerializeField, Min(1)] private int outputQuantity = 1;
        [SerializeField, Min(0.1f)] private float seconds = 5f;
        public string Id => id;
        public IReadOnlyList<RecipeIngredient> Ingredients => ingredients;
        public ItemDefinition Output => output;
        public int OutputQuantity => outputQuantity;
        public float Seconds => seconds;

        public void Configure(string stableId, RecipeIngredient[] inputs, ItemDefinition result, int amount, float duration)
        {
            if (string.IsNullOrWhiteSpace(stableId) || inputs == null || inputs.Length == 0 || result == null || amount < 1 || duration <= 0f)
                throw new ArgumentException("Recipe requires valid inputs, output and duration.");
            var seen = new HashSet<string>();
            foreach (var ingredient in inputs)
                if (ingredient.item == null || ingredient.quantity < 1 || !seen.Add(ingredient.item.Id))
                    throw new ArgumentException("Recipe ingredients must have positive quantities and unique item IDs.");
            id = stableId;
            ingredients = (RecipeIngredient[])inputs.Clone();
            output = result;
            outputQuantity = amount;
            seconds = duration;
        }
    }
}
