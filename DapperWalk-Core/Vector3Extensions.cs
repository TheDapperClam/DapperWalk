using UnityEngine;

namespace DapperWalk.Core {
    public static class Vector3Extensions {
        public static System.Numerics.Vector3 ToNumerics ( this Vector3 vec3 ) {
            return new ( vec3.x, vec3.y, vec3.z );
        }

        public static Vector3 ToUnity (this System.Numerics.Vector3 vec3) {
            return new ( vec3.X, vec3.Y, vec3.Z );
        }
    }
}
