using Dinner.ServiceDefaults;
var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build().MapDinnerServiceDefaults("recipe-service");
app.UseCors();

var recipes = new List<Recipe>
{
    new("tomato-chickpea-pasta", "Tomato chickpea pasta", "dinner", 30, ["vegetarian", "high-fibre"], [new("Pasta", 250, "g", null), new("Tinned tomatoes", 2, "item", "off-tomatoes"), new("Chickpeas", 1, "item", "off-chickpeas")]),
    new("vegetable-stir-fry", "Vegetable stir-fry", "dinner", 25, ["vegan", "quick"], [new("Rice", 250, "g", null), new("Mixed vegetables", 500, "g", null), new("Soy sauce", 30, "ml", null)]),
    new("lentil-soup", "Red lentil soup", "dinner", 40, ["vegan", "batch-friendly"], [new("Red lentils", 250, "g", null), new("Tinned tomatoes", 1, "item", "off-tomatoes"), new("Vegetable stock", 750, "ml", null)]),
    new("overnight-oats", "Berry overnight oats", "breakfast", 10, ["vegetarian", "quick"], [new("Oats", 160, "g", null), new("Milk", 500, "ml", null), new("Berries", 200, "g", null)])
};
app.MapGet("/api/v1/recipes", (string? ingredient, int? maxMinutes, string? mealType, string? tag) => Results.Ok(recipes.Where(r => (string.IsNullOrWhiteSpace(ingredient) || r.Ingredients.Any(i => i.Name.Contains(ingredient, StringComparison.OrdinalIgnoreCase))) && (!maxMinutes.HasValue || r.TotalMinutes <= maxMinutes) && (string.IsNullOrWhiteSpace(mealType) || r.MealType.Equals(mealType, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(tag) || r.DietaryTags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase))))));
app.MapGet("/api/v1/recipes/{id}", (string id) => recipes.FirstOrDefault(r => r.Id == id) is { } recipe ? Results.Ok(recipe) : Results.NotFound());
app.MapGet("/mcp/resources/recipes/catalog", () => Results.Ok(new { uri = "recipes://catalog", recipes }));
app.MapPost("/mcp/tools/find_recipes", (RecipeSearch input) => Results.Ok(new { structuredContent = recipes.Where(r => (string.IsNullOrWhiteSpace(input.Ingredient) || r.Ingredients.Any(i => i.Name.Contains(input.Ingredient, StringComparison.OrdinalIgnoreCase))) && (!input.MaxMinutes.HasValue || r.TotalMinutes <= input.MaxMinutes) && (string.IsNullOrWhiteSpace(input.MealType) || r.MealType.Equals(input.MealType, StringComparison.OrdinalIgnoreCase))).ToArray() }));
app.MapPost("/mcp/tools/get_recipe", (RecipeLookup input) => recipes.FirstOrDefault(r => r.Id == input.RecipeId) is { } recipe ? Results.Ok(new { structuredContent = recipe }) : Results.NotFound());
app.MapPost("/mcp/tools/scale_recipe", (ScaleRecipe input) => recipes.FirstOrDefault(r => r.Id == input.RecipeId) is { } recipe && input.Servings > 0 ? Results.Ok(new { structuredContent = recipe with { Ingredients = recipe.Ingredients.Select(i => i with { Quantity = i.Quantity * input.Servings / 2m }).ToArray() } }) : Results.ValidationProblem(new Dictionary<string, string[]> { ["recipe"] = ["A recipe and positive serving count are required."] }));
app.MapGet("/mcp/capabilities", () => Results.Ok(new { version = "v1", resources = new[] { "recipes://catalog" }, tools = new[] { "find_recipes", "get_recipe", "scale_recipe" } }));
app.Run();

record Recipe(string Id, string Name, string MealType, int TotalMinutes, string[] DietaryTags, RecipeIngredient[] Ingredients);
record RecipeIngredient(string Name, decimal Quantity, string Unit, string? NutritionProductId);
record RecipeSearch(string? Ingredient, int? MaxMinutes, string? MealType);
record RecipeLookup(string RecipeId);
record ScaleRecipe(string RecipeId, int Servings);
