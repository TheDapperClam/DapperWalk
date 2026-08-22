using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public abstract class HudElement : MonoBehaviour {
        private const string UI_PARENT_NAME = "Will Fade With Ending";

        protected virtual void Awake () {
            GameObject hudParent = GameObject.Find ( UI_PARENT_NAME );
            transform.parent = hudParent.transform;
        }
    }
}
