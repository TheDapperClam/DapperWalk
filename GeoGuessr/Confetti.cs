using DapperWalk.Core;
using System.Linq;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class Confetti : MonoBehaviour {
        private const string CONFETTI_AUDIO_CLIP_NAME = "sfx_challengeWin_confetti_01";
        private const string CONFETTI_PARTICLES_NAME = "RewardConfettiParticle";

        private ResourceRepository resourceRepository;

        public void Play () {
            GetComponent<AudioSource> ().Play ();
            GetComponentInChildren<ParticleSystem> ().Play ();
        }

        private void Awake () {
            resourceRepository = Singleton.Summon<ResourceRepository> ();

            AudioClip sfx = resourceRepository.Get<AudioClip> ( CONFETTI_AUDIO_CLIP_NAME );
            AudioSource audioSource = gameObject.AddComponent<AudioSource> ();
            audioSource.clip = sfx;

            Instantiate ( GameObject.Find ( CONFETTI_PARTICLES_NAME ), transform, false );
        }
    }
}
