namespace ManyWinters.Core.Items;

public sealed record RecipeDefinition(ItemKindId Output, ItemKindId InputItem, int InputAmount);
