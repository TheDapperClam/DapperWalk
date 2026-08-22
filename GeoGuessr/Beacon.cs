using DapperWalk.Core;
using System.Linq;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class Beacon : MonoBehaviour {
        private const float BEACON_DOT_SCALE = 0.5f;
        private const string BEACON_MATERIAL_NAME = "Sprite-Unlit-Default";
        private const float BEACON_MAX_HEIGHT = 100.0f;
        private const float BEACON_WIDTH = 0.25f;

        public Color Color {
            set {
                Color endColor = value;
                endColor.a = 0f;

                GetComponent<LineRenderer> ().SetColors ( value, endColor );
                dot.GetComponent<Renderer> ().material.color = value;
            }
        }

        private GameObject dot;
        private ResourceRepository resourceRepository;

        private void Awake () {
            resourceRepository = Singleton.Summon<ResourceRepository> ();

            Material mat = resourceRepository.Get<Material> ( BEACON_MATERIAL_NAME );
            LineRenderer line = gameObject.AddComponent<LineRenderer> ();
            line.useWorldSpace = false;
            line.widthMultiplier = BEACON_WIDTH;
            line.material = mat;
            line.SetPosition ( 0, Vector3.zero );
            line.SetPosition ( 1, Vector3.up * BEACON_MAX_HEIGHT );

            dot = GameObject.CreatePrimitive ( PrimitiveType.Sphere );
            dot.transform.parent = transform;
            dot.transform.localScale *= BEACON_DOT_SCALE;
            dot.transform.localPosition = Vector3.zero;
            Destroy ( dot.GetComponent<Collider> () );

            Renderer rend = dot.GetComponent<Renderer> ();
            rend.material = mat;
        }
    }
}
