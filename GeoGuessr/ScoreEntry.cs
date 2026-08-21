namespace DapperWalk.GeoGuessr {
    public struct ScoreEntry ( string name, uint netId ) {
        public string Name { get; set; } = name;
        public uint NetId { get; set; } = netId;
        public float Score { get; set; }
    }
}
