using UnityEngine;

namespace DapperWalk.Core {
    public class PlayerCharacterService : MonoBehaviour {
        public PlayerCharacter? GetLocalPlayerCharacter () {
            PlayerCharacter[] pcs = GetPlayerCharacters ();

            if (pcs == null || pcs.Length <= 0) {
                return null;
            }
            foreach (PlayerCharacter pc in pcs) {
                if (pc.isLocalPlayer) {
                    return pc;
                }
            }
            return null;
        }

        public PlayerCharacter? GetPlayerCharacter ( uint netId ) {
            PlayerCharacter[] pcs = GetPlayerCharacters ();

            if (pcs == null || pcs.Length <= 0) {
                return null;
            }
            foreach (PlayerCharacter pc in pcs) {
                if (pc.netId == netId) {
                    return pc;
                }
            }
            return null;
        }

        public PlayerCharacter[] GetPlayerCharacters () {
            PlayerCharacter[] pcs = FindObjectsByType<PlayerCharacter> ( FindObjectsSortMode.None );
            return pcs;
        }
    }
}
