using DapperWalk.Core;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public struct GeoGuessrPoint ( PlayerCharacter pc ) {
        // Unity Vector3 causes a serialization loop because of IL2CPP, so we'll use System.Numerics instead
        public System.Numerics.Vector3 Position { get; set; } = pc.transform.position.ToNumerics ();
        public uint OwnerNetId { get; set; } = pc.netId;
        public System.Numerics.Vector3 SpectatePosition { get; set; } = pc.cameraTransform.position.ToNumerics ();

        public Vector3 GetPosition () {
            return Position.ToUnity ();
        }

        public Vector3 GetSpectatePosition () {
            return SpectatePosition.ToUnity ();
        }
    }
}
