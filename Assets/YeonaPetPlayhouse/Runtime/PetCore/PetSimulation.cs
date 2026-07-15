using System;

namespace YeonaPetPlayhouse.PetCore
{
    public enum PetMood
    {
        Happy = 0,
        WantsFood = 1,
        WantsBath = 2,
        Sleepy = 3,
        Sleeping = 4
    }

    /// <summary>
    /// 무실패(no-fail) 펫 돌보기 시뮬레이션. 만 4세 대상 장난감 상자 설계라
    /// 벌칙·게임오버·타이머가 없고, 욕구가 높아지면 펫이 원하는 것을 "알려줄" 뿐이다.
    /// 시간 개념은 Tick(elapsedSeconds)으로만 들어오는 순수 C# — EditMode/mono 테스트 가능.
    /// </summary>
    [Serializable]
    public sealed class PetSimulation
    {
        public const float NeedMaximum = 100f;
        // 욕구가 이 값을 넘으면 펫이 말풍선으로 원하는 것을 표현한다.
        public const float WantThreshold = 60f;

        // 분당 상승량. 한 번 케어하면 10~20분은 행복하게 노는 완만한 속도.
        public const float HungerPerMinute = 4f;
        public const float DirtPerMinute = 3f;
        public const float SleepinessPerMinute = 5f;

        public const float FeedRelief = 45f;
        public const float ScrubRelief = 12f;
        public const float SleepReliefPerSecond = 12f;

        private float hunger;
        private float dirt;
        private float sleepiness;
        private bool sleeping;

        public float Hunger { get { return hunger; } }
        public float Dirt { get { return dirt; } }
        public float Sleepiness { get { return sleepiness; } }
        public bool IsSleeping { get { return sleeping; } }

        /// <summary>새 게임 시작 상태: 배고픔만 살짝 있어서 첫 상호작용을 자연스럽게 유도.</summary>
        public static PetSimulation CreateFresh()
        {
            PetSimulation simulation = new PetSimulation();
            simulation.hunger = WantThreshold + 5f;
            simulation.dirt = 20f;
            simulation.sleepiness = 10f;
            return simulation;
        }

        public static PetSimulation Restore(float hunger, float dirt, float sleepiness)
        {
            PetSimulation simulation = new PetSimulation();
            simulation.hunger = Clamp(hunger);
            simulation.dirt = Clamp(dirt);
            simulation.sleepiness = Clamp(sleepiness);
            return simulation;
        }

        public void Tick(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f)
            {
                return;
            }

            float minutes = elapsedSeconds / 60f;
            if (sleeping)
            {
                // 자는 동안엔 졸림만 회복되고 다른 욕구는 멈춘다 (아이가 불 끄고 기다리는 시간이
                // 손해로 느껴지지 않도록).
                sleepiness = Clamp(sleepiness - SleepReliefPerSecond * elapsedSeconds);
                if (sleepiness <= 0f)
                {
                    sleeping = false;
                }

                return;
            }

            hunger = Clamp(hunger + HungerPerMinute * minutes);
            dirt = Clamp(dirt + DirtPerMinute * minutes);
            sleepiness = Clamp(sleepiness + SleepinessPerMinute * minutes);
        }

        /// <summary>당근/간식 하나를 먹인다. 배고프지 않아도 조금은 먹는다(항상 성공).</summary>
        public void Feed()
        {
            sleeping = false;
            hunger = Clamp(hunger - FeedRelief);
        }

        /// <summary>목욕 문지르기 한 번. 여러 번 문지르면 깨끗해진다.</summary>
        public void Scrub()
        {
            sleeping = false;
            dirt = Clamp(dirt - ScrubRelief);
        }

        public bool IsClean
        {
            get { return dirt < 10f; }
        }

        public void StartSleep()
        {
            sleeping = true;
        }

        public void WakeUp()
        {
            sleeping = false;
        }

        /// <summary>
        /// 지금 펫이 보여줄 표정/상태. 가장 높은 욕구 하나만 표현한다 —
        /// 4세에게 동시에 두 가지를 요구하지 않기 위한 규칙.
        /// </summary>
        public PetMood CurrentMood
        {
            get
            {
                if (sleeping)
                {
                    return PetMood.Sleeping;
                }

                float strongest = WantThreshold;
                PetMood mood = PetMood.Happy;
                if (hunger > strongest)
                {
                    strongest = hunger;
                    mood = PetMood.WantsFood;
                }

                if (dirt > strongest)
                {
                    strongest = dirt;
                    mood = PetMood.WantsBath;
                }

                if (sleepiness > strongest)
                {
                    mood = PetMood.Sleepy;
                }

                return mood;
            }
        }

        private static float Clamp(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > NeedMaximum ? NeedMaximum : value;
        }
    }
}
