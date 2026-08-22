using System.Linq;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class Confetti : MonoBehaviour {
        private const string CONFETTI_AUDIO_CLIP_NAME = "sfx_challengeWin_confetti_01";
        private const string CONFETTI_PARTICLES_NAME = "RewardConfettiParticle";

        public void Play () {
            GetComponent<AudioSource> ().Play ();
            GetComponentInChildren<ParticleSystem> ().Play ();
        }

        private void Awake () {
            AudioClip sfx = Resources.FindObjectsOfTypeAll<AudioClip> ().First ( c => c.name == CONFETTI_AUDIO_CLIP_NAME );
            AudioSource audioSource = gameObject.AddComponent<AudioSource> ();
            audioSource.clip = sfx;

            Instantiate ( GameObject.Find ( CONFETTI_PARTICLES_NAME ), transform, false );
        }
    }
}
