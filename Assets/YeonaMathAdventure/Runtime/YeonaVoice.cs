using System.Text;
using UnityEngine;

namespace YeonaMathAdventure
{
    /// <summary>
    /// 글을 읽지 못하는 아이를 위한 음성 안내.
    /// 우선순위: ① Resources/YeonaMathAdventure/Voice/{clipKey} 녹음 클립이 있으면 재생,
    /// ② 없으면 안드로이드 기기 내장 한국어 TTS로 읽어 준다. 에디터/기타 플랫폼에서는
    /// 로그만 남긴다. 네트워크·API 키를 쓰지 않는 완전 오프라인 구성이며, 음성 경로가
    /// 실패해도 게임 진행을 막지 않는다.
    /// </summary>
    public static class YeonaVoice
    {
        private const string ClipResourceRoot = "YeonaMathAdventure/Voice/";

        private static AudioSource clipSource;
        private static bool initialized;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject tts;
        private static volatile bool ttsReady;
        private static TtsInitListener initListener;
#endif

        public static void Initialize(MonoBehaviour host)
        {
            if (initialized || host == null)
            {
                return;
            }

            initialized = true;
            clipSource = host.gameObject.GetComponent<AudioSource>();
            if (clipSource == null)
            {
                clipSource = host.gameObject.AddComponent<AudioSource>();
            }

            clipSource.playOnAwake = false;
            clipSource.spatialBlend = 0f;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                initListener = new TtsInitListener();
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(delegate
                    {
                        tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, initListener);
                    }));
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Yeona Voice] TTS 초기화 실패: " + exception.Message);
                tts = null;
            }
#endif
        }

        public static void Speak(string koreanText, bool interrupt = true)
        {
            Speak(null, koreanText, interrupt);
        }

        /// <summary>
        /// clipKey가 있고 같은 이름의 녹음 클립이 Resources에 있으면 클립을, 아니면 TTS를 쓴다.
        /// interrupt가 false면 진행 중인 안내 뒤에 이어서 말한다(TTS 큐잉).
        /// </summary>
        public static void Speak(string clipKey, string koreanText, bool interrupt = true)
        {
            if (!initialized)
            {
                return;
            }

            if (!string.IsNullOrEmpty(clipKey) && TryPlayClip(clipKey, interrupt))
            {
                return;
            }

            string sanitized = Sanitize(koreanText);
            if (string.IsNullOrEmpty(sanitized))
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (tts == null || !ttsReady)
            {
                return;
            }

            try
            {
                int queueMode = interrupt ? 0 : 1; // QUEUE_FLUSH : QUEUE_ADD
                tts.Call<int>("speak", sanitized, queueMode, (AndroidJavaObject)null, "yeona_voice");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Yeona Voice] speak 실패: " + exception.Message);
            }
#else
            Debug.Log("[Yeona Voice] " + (clipKey ?? "-") + " :: " + sanitized);
#endif
        }

        public static void Shutdown()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (tts != null)
            {
                try
                {
                    tts.Call<int>("stop");
                    tts.Call("shutdown");
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("[Yeona Voice] shutdown 실패: " + exception.Message);
                }

                tts.Dispose();
                tts = null;
                ttsReady = false;
            }
#endif
            initialized = false;
            clipSource = null;
        }

        private static bool TryPlayClip(string clipKey, bool interrupt)
        {
            if (clipSource == null)
            {
                return false;
            }

            AudioClip clip = Resources.Load<AudioClip>(ClipResourceRoot + clipKey);
            if (clip == null)
            {
                return false;
            }

            if (interrupt)
            {
                clipSource.Stop();
                clipSource.clip = clip;
                clipSource.Play();
            }
            else
            {
                clipSource.PlayOneShot(clip);
            }

            return true;
        }

        /// <summary>
        /// UI 문자열에는 별(★), 구분점(·), 줄바꿈 같은 장식이 섞여 있어 TTS가 어색하게
        /// 읽는다. 말로 전달할 내용만 남긴다. 순수 함수라 EditMode 테스트로 검증한다.
        /// </summary>
        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            bool lastWasSpace = true;
            for (int index = 0; index < text.Length; index++)
            {
                char value = text[index];
                if (value == '★' || value == '☆' || value == '●' || value == '▰' || value == '◆')
                {
                    continue;
                }

                if (value == '\n' || value == '\r' || value == '\t' || value == '·')
                {
                    value = ' ';
                }

                if (value == ' ')
                {
                    if (lastWasSpace)
                    {
                        continue;
                    }

                    lastWasSpace = true;
                }
                else
                {
                    lastWasSpace = false;
                }

                builder.Append(value);
            }

            return builder.ToString().Trim();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class TtsInitListener : AndroidJavaProxy
        {
            public TtsInitListener() : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
            }

            // TextToSpeech.OnInitListener — 자바 쪽에서 호출된다.
            public void onInit(int status)
            {
                if (status != 0 || tts == null) // 0 == TextToSpeech.SUCCESS
                {
                    Debug.LogWarning("[Yeona Voice] TTS 엔진 초기화 실패 status=" + status);
                    return;
                }

                try
                {
                    using (AndroidJavaObject korean = new AndroidJavaObject("java.util.Locale", "ko", "KR"))
                    {
                        int languageResult = tts.Call<int>("setLanguage", korean);
                        if (languageResult < 0) // LANG_MISSING_DATA(-1), LANG_NOT_SUPPORTED(-2)
                        {
                            Debug.LogWarning("[Yeona Voice] 한국어 TTS 미지원 기기 result=" + languageResult);
                            return;
                        }
                    }

                    tts.Call<int>("setSpeechRate", 0.9f);
                    tts.Call<int>("setPitch", 1.05f);
                    ttsReady = true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("[Yeona Voice] TTS 설정 실패: " + exception.Message);
                }
            }
        }
#endif
    }
}
