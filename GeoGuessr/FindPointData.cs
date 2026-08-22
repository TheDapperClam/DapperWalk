namespace DapperWalk.GeoGuessr {
    public struct FindPointData ( GeoGuessrPoint point, float time ) {
        public GeoGuessrPoint Point { get; set; } = point;
        public float Time { get; set; } = time;
    }
}
