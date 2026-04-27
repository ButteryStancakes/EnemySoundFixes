using UnityEngine;

namespace EnemySoundFixes
{
    public class RadioChatter : MonoBehaviour
    {
        internal WalkieTalkie walkieTalkie;
        AudioSource audio;

        void Awake()
        {
            audio = new GameObject("EnemySoundFixes_WalkieTalkieTalkingNotHeld").AddComponent<AudioSource>();
            audio.transform.SetParent(transform, false);
            audio.clip = GetComponent<WalkieTalkie>().talkingOnWalkieTalkieNotHeldSFX;
            audio.loop = true;

            AudioSource audioSource = GetComponent<AudioSource>();
            audio.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            audio.SetCustomCurve(AudioSourceCurveType.SpatialBlend, audioSource.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
            audio.dopplerLevel = audioSource.dopplerLevel;
            audio.spread = audioSource.spread;
            audio.rolloffMode = AudioRolloffMode.Custom;
            audio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            audio.maxDistance = audioSource.maxDistance;
        }

        void Update()
        {
            if (walkieTalkie == null)
            {
                walkieTalkie = GetComponent<WalkieTalkie>();
                if (walkieTalkie == null)
                {
                    enabled = false;
                    return;
                }
            }

            if (!walkieTalkie.isBeingUsed || !Plugin.configWalkieHearsTalkies.Value)
            {
                SetPlaying(false);
                return;
            }

            if (GameNetworkManager.Instance?.localPlayerController == null)
                return;

            if (GameNetworkManager.Instance.localPlayerController.holdingWalkieTalkie || (GameNetworkManager.Instance.localPlayerController.isPlayerDead && GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript != null && GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript.holdingWalkieTalkie))
            {
                SetPlaying(false);
                return;
            }

            bool radiosInUse = false;
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                if (StartOfRound.Instance.allPlayerScripts[i].holdingWalkieTalkie && StartOfRound.Instance.allPlayerScripts[i].speakingToWalkieTalkie)
                {
                    // don't trigger when there's only a single walkie in use
                    if (StartOfRound.Instance.allPlayerScripts[i].currentlyHeldObjectServer != walkieTalkie)
                        continue;

                    radiosInUse = true;
                    break;
                }
            }

            SetPlaying(radiosInUse);
        }

        public void SetPlaying(bool play)
        {
            if (audio == null)
                return;

            if (play)
            {
                if (audio.isPlaying)
                    return;

                audio.pitch = Random.Range(0.94f, 1.06f);
                audio.Play();
                audio.time = Random.Range(0f, audio.clip.length - 0.1f);
            }
            else if (audio.isPlaying)
                audio.Stop();
        }

        void OnDisable()
        {
            SetPlaying(false);
        }
    }
}
