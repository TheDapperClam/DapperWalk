using System.Linq;
using TMPro;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class HudText : HudElement {
        private const string TEXT_FONT_NAME = "Courier Prime Sans Bold SDF";

        public TextMeshProUGUI Text { get; private set; }

        protected override void Awake () {
            base.Awake ();

            TMP_FontAsset font = Resources.FindObjectsOfTypeAll<TMP_FontAsset> ().First ( f => f.name == TEXT_FONT_NAME );
            Text = gameObject.AddComponent<TextMeshProUGUI> ();
            Text.font = font;

            RectTransform rectTransform = GetComponent<RectTransform> ();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
        }
    }
}
