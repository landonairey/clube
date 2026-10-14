using System.Globalization;
using Clube.Core;
using Clube.Game;

namespace Clube.Debug
{
    /// <summary>
    /// Typed cheat commands for testing (GL43), run from the lab panel's Cheats section:
    /// <c>coins 500</c> adds coins, <c>give chalcopyrite 300</c> adds items (any id or name
    /// from the <see cref="ItemCatalog"/>, one if no count), <c>help</c> lists them. Lab-only
    /// (<c>Clube.Debug</c>), so the Game scene never has them (A10).
    /// </summary>
    public static class CheatCommands
    {
        public const string Help = "coins <amount> · give <item> [count] · help";

        /// <summary>Runs one command line; returns what happened, for the panel to show.</summary>
        public static string Run(string line, PlayerInventory inventory, PlayerWallet wallet, ItemCatalog catalog)
        {
            string[] words = (line ?? "").Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || words[0].ToLowerInvariant() == "help")
            {
                return Help;
            }

            switch (words[0].ToLowerInvariant())
            {
                case "coins":
                case "money":
                    if (words.Length < 2 || !int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int coins) || coins <= 0)
                    {
                        return "Usage: coins <amount>";
                    }
                    return GiveCoins(wallet, coins);

                case "give":
                    if (words.Length < 2)
                    {
                        return "Usage: give <item> [count]";
                    }
                    // A trailing number is the count; everything between is the item's name.
                    int count = 1;
                    int nameEnd = words.Length;
                    if (words.Length > 2 && int.TryParse(words[words.Length - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        count = parsed;
                        nameEnd--;
                    }
                    string name = string.Join(" ", words, 1, nameEnd - 1);
                    ItemDefinition item = catalog != null ? catalog.Find(name) : null;
                    if (item == null)
                    {
                        return $"No item called \"{name}\"";
                    }
                    return GiveItems(inventory, item, count);

                default:
                    return $"Unknown command \"{words[0]}\". {Help}";
            }
        }

        public static string GiveCoins(PlayerWallet wallet, int coins)
        {
            if (wallet == null || wallet.Wallet == null)
            {
                return "The player has no wallet (Play mode only)";
            }
            wallet.Wallet.Earn(coins);
            return $"+{coins} coins ({wallet.Wallet.Coins})";
        }

        public static string GiveItems(PlayerInventory inventory, ItemDefinition item, int count)
        {
            if (inventory == null || inventory.Inventory == null)
            {
                return "The player has no inventory (Play mode only)";
            }
            if (item == null || count <= 0)
            {
                return "Nothing to give";
            }
            int left = inventory.Inventory.Add(item, count);
            int given = count - left;
            return left == 0 ? $"+{given} {item.DisplayName}" : $"+{given} {item.DisplayName}; {left} didn't fit";
        }
    }
}
