using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Hands over from one of the player's screens to another (list inventory prototype): the journal keys (Q, J), the
    /// inventory (E) and the map (M) close whichever of the inventory, the field log, the map, a station, a chest or a
    /// trader is open and open their own screen, so no screen has to be closed first. Other screens that hold the
    /// player in a menu (rest, an open clam, the pause menu) still block.
    /// </summary>
    public static class ScreenSwitch
    {
        /// <summary>True while one of the screens that can be switched away from is open, other than <paramref name="except"/>.</summary>
        public static bool AnyOpen(GameObject player, Object except = null)
        {
            return IsOpen(player.GetComponent<InventoryScreen>(), except)
                || IsOpen(player.GetComponent<FieldLogScreen>(), except)
                || IsOpen(player.GetComponent<WorldMapScreen>(), except)
                || IsOpen(player.GetComponent<StationPanel>(), except)
                || IsOpen(player.GetComponent<ChestPanel>(), except)
                || IsOpen(player.GetComponent<MerchantPanel>(), except);
        }

        /// <summary>Closes every switchable screen but <paramref name="except"/>.</summary>
        public static void CloseOthers(GameObject player, Object except)
        {
            if (player.GetComponent<InventoryScreen>() is InventoryScreen inventory && inventory != except)
            {
                inventory.Close();
            }
            if (player.GetComponent<FieldLogScreen>() is FieldLogScreen log && log != except)
            {
                log.Close();
            }
            if (player.GetComponent<WorldMapScreen>() is WorldMapScreen map && map != except)
            {
                map.Close();
            }
            if (player.GetComponent<StationPanel>() is StationPanel station && station != except)
            {
                station.Close();
            }
            if (player.GetComponent<ChestPanel>() is ChestPanel chest && chest != except)
            {
                chest.Close();
            }
            if (player.GetComponent<MerchantPanel>() is MerchantPanel trader && trader != except)
            {
                trader.Close();
            }
        }

        private static bool IsOpen(Component screen, Object except)
        {
            if (screen == null || screen == except)
            {
                return false;
            }
            return screen switch
            {
                InventoryScreen s => s.IsOpen,
                FieldLogScreen s => s.IsOpen,
                WorldMapScreen s => s.IsOpen,
                StationPanel s => s.IsOpen,
                ChestPanel s => s.IsOpen,
                MerchantPanel s => s.IsOpen,
                _ => false,
            };
        }
    }
}
