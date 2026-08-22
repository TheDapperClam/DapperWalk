using DapperWalk.Core;
using System;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class HudTimer : HudText {
        private const string WARNING_AUDIO_CLIP_NAME = "ui_text_chat_send";

        private Action? callback;
        private ResourceRepository resourceRepository;
        private float targetTime;
        private AudioSource warningAudio;
        private int warningRemainingTime;

        public void Play ( float time, Action? callback, int warningRemainingTime = 5 ) {
            this.callback = callback;
            this.warningRemainingTime = warningRemainingTime;
            targetTime = Time.time + time;
            gameObject.SetActive ( true );
        }

        protected override void Awake () {
            base.Awake ();

            resourceRepository = Singleton.Summon<ResourceRepository> ();

            AudioClip clip = resourceRepository.Get<AudioClip> ( WARNING_AUDIO_CLIP_NAME );
            warningAudio = gameObject.AddComponent<AudioSource> ();
            warningAudio.clip = clip;
            warningAudio.spatialBlend = 0f;
        }

        private void Update () {
            int remainingTime = Mathf.CeilToInt ( targetTime - Time.time );

            if (remainingTime > 0) {
                string currentText = Text.text;
                Text.text = remainingTime.ToString ();

                if (remainingTime <= warningRemainingTime && currentText != Text.text) {
                    warningAudio.Play ();
                }
                return;
            }

            gameObject.SetActive ( false );
            callback?.Invoke ();
        }
    }
}
