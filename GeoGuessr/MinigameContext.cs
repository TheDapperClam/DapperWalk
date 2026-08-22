using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class MinigameContext : MonoBehaviour {
        public GeoGuessrPoint PointToFind { get; set; }

        public void Reset () {
            PointToFind = default;
        }
    }
}
