using System.Collections.Generic;

namespace DapperWalk.GeoGuessr {
    public struct RevealData (Dictionary<uint, GeoGuessrPoint> guessedPoints, string score, GeoGuessrPoint targetPoints, float time, bool wasLastRound, uint winnerNetId) {
        public Dictionary<uint, GeoGuessrPoint> GuessedPoints { get; set; } = guessedPoints;
        public string Score { get; set; } = score;
        public GeoGuessrPoint TargetPoint { get; set; } = targetPoints;
        public float Time { get; set; } = time;
        public bool WasLastRound { get; set; } = wasLastRound;
        public uint WinnerNetId { get; set; } = winnerNetId;
    }
}
