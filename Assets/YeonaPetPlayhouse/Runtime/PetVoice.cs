using System.Collections.Generic;
using UnityEngine;

namespace YeonaPetPlayhouse
{
    /// <summary>
    /// 녹음 클립 전용 음성 재생기. 기기 TTS는 쓰지 않는다 —
    /// 목소리는 Documentation/VOICE_SCRIPT.md 대본으로 AI 생성해
    /// Resources/YeonaPetPlayhouse/Voice/{key}.(wav|ogg|mp3) 로 넣는다.
    /// 클립이 없으면 조용히 넘어가서 게임 진행을 절대 막지 않는다.
    /// </summary>
    public static class PetVoice
    {
        private const string ClipResourceRoot = "YeonaPetPlayhouse/Voice/";

        private static AudioSource source;
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static void Initialize(MonoBehaviour host)
        {
            if (source != null || host == null)
            {
                return;
            }

            source = host.gameObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = host.gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        /// <summary>키에 해당하는 클립 재생. interrupt=false면 겹쳐서 재생(짧은 리액션용).</summary>
        public static bool Play(string clipKey, bool interrupt = true)
        {
            if (source == null || string.IsNullOrEmpty(clipKey))
            {
                return false;
            }

            AudioClip clip;
            if (!cache.TryGetValue(clipKey, out clip))
            {
                clip = Resources.Load<AudioClip>(ClipResourceRoot + clipKey);
                cache[clipKey] = clip;
                if (clip == null)
                {
                    Debug.Log("[Yeona Pet] 음성 클립 없음(무시하고 진행): " + clipKey);
                }
            }

            if (clip == null)
            {
                return false;
            }

            if (interrupt)
            {
                source.Stop();
                source.clip = clip;
                source.Play();
            }
            else
            {
                source.PlayOneShot(clip);
            }

            return true;
        }

        /// <summary>후보 키 중 하나를 랜덤 재생 — 같은 리액션도 매번 다른 말로.</summary>
        public static void PlayOneOf(bool interrupt, params string[] clipKeys)
        {
            if (clipKeys == null || clipKeys.Length == 0)
            {
                return;
            }

            Play(clipKeys[Random.Range(0, clipKeys.Length)], interrupt);
        }
    }
}
