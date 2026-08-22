using System;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class HudTimer : HudText {
        private Action? callback;
        private float targetTime;

        public void Play ( float time, Action? callback ) {
            this.callback = callback;
            targetTime = Time.time + time;
            gameObject.SetActive ( true );
        }

        private void Update () {
            float remainingTime = targetTime - Time.time;

            if (remainingTime > 0f) {
                Text.text = Mathf.CeilToInt ( remainingTime ).ToString ();
                return;
            }

            gameObject.SetActive ( false );
            callback?.Invoke ();
        }
    }
}
