namespace DapperWalk.GeoGuessr {
    public interface IMinigame {
        public int MinimumPlayerCount { get; set; }

        public void StartMinigame ();
    }
}
