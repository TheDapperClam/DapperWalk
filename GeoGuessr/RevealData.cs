using System.Collections.Generic;

namespace DapperWalk.GeoGuessr {
    public struct RevealData (Dictionary<uint, GeoGuessrPoint> guessedPoints, string score, GeoGuessrPoint targetPoints, bool wasLastRound, uint winnerNetId) {
        public Dictionary<uint, GeoGuessrPoint> GuessedPoints { get; set; } = guessedPoints;
        public string Score { get; set; } = score;
        public GeoGuessrPoint TargetPoint { get; set; } = targetPoints;
        public bool WasLastRound { get; set; } = wasLastRound;
        public uint WinnerNetId { get; set; } = winnerNetId;
    }
}
