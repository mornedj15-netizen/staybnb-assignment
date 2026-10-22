# Smart Pantry Manager

An Android app (Java) that helps reduce food waste. The user records the ingredients
they already have at home, and the app suggests only the recipes they can cook right now,
with no shopping trip needed.

Built for Mobile App Development 700 (Richfield Graduate Institute of Technology).

## Features
- Add, edit and delete pantry items (name, quantity, unit, optional expiry date)
- Pantry list screen (RecyclerView with a custom adapter)
- 19 pre-loaded recipes, seeded on first run
- **Strict matching:** a recipe is suggested only if every ingredient is in the pantry
  in at least the required quantity. It handles singular/plural names ("tomato" vs
  "tomatoes") and unit differences (kg vs g, l vs ml)
- Recipe detail screen with the full ingredient list and method
- Settings screen: toggle that highlights expired or soon-to-expire items
- Input validation and a friendly message when no recipes match

## Database choice: SQLite
I chose SQLite (using `SQLiteOpenHelper`) because:
- It is stored locally on the device, so the app works offline with no backend or account
- It matches the persistent-data approach taught in the module
- The data is small and structured (pantry items, recipes, recipe ingredients), which
  suits relational tables with a one-to-many relationship between recipes and ingredients

Tables: `pantry_items`, `recipes`, `recipe_ingredients`.

## Setup and run instructions
1. Install [Android Studio](https://developer.android.com/studio).
2. Clone the repository: `git clone https://github.com/mornedj15-netizen/staybnb-assignment/tree/master`
3. In Android Studio choose **File → Open** and select the cloned folder.
4. Wait for Gradle to finish syncing.
5. Create an emulator in **Tools → Device Manager** (or connect a phone with USB debugging).
6. Press **Run**. The recipes are loaded automatically the first time the app opens.

Minimum SDK: API 24.

## Project structure
- `database/` - `DatabaseHelper` (CRUD) and `RecipeSeeder`
- `model/` - `PantryItem`, `Recipe`, `RecipeIngredient`
- `adapter/` - `PantryAdapter`, `RecipeAdapter`
- `ui/` - the Activities (pantry list, add/edit, suggested recipes, recipe detail, settings)
- `util/` - `RecipeMatcher` (strict-matching logic), `NavigationHelper`

## Author
[Morne de Jager 402411727]
