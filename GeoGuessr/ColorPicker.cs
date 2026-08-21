using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class ColorPicker {
        private readonly Color[] colors;

        private int index;

        public ColorPicker ( Color[] colors ) {
            this.colors = colors;
        }

        public Color Pick () {
            Color color = colors[index];

            if (++index >= colors.Length) {
                index = 0;
            }
            return color;
        }
    }
}
