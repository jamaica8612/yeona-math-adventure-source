using NUnit.Framework;
using YeonaPetPlayhouse.PetCore;

namespace YeonaPetPlayhouse.Tests
{
    public sealed class PetCoreTests
    {
        [Test]
        public void FreshPet_AsksForFoodFirst()
        {
            PetSimulation pet = PetSimulation.CreateFresh();
            Assert.That(pet.CurrentMood, Is.EqualTo(PetMood.WantsFood));
        }

        [Test]
        public void Feeding_AlwaysSucceedsAndCalmsHunger()
        {
            PetSimulation pet = PetSimulation.CreateFresh();
            pet.Feed();
            Assert.That(pet.CurrentMood, Is.EqualTo(PetMood.Happy));
            Assert.That(pet.Hunger, Is.LessThan(PetSimulation.WantThreshold));

            // 배고프지 않아도 벌칙 없이 그냥 먹는다 (무실패 규칙).
            pet.Feed();
            pet.Feed();
            Assert.That(pet.Hunger, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Needs_RiseSlowlyOverTime()
        {
            PetSimulation pet = PetSimulation.Restore(0f, 0f, 0f);
            pet.Tick(60f * 10f); // 10분 방치
            Assert.That(pet.Hunger, Is.EqualTo(PetSimulation.HungerPerMinute * 10f).Within(0.01f));
            Assert.That(pet.Dirt, Is.EqualTo(PetSimulation.DirtPerMinute * 10f).Within(0.01f));
            Assert.That(pet.Sleepiness, Is.EqualTo(PetSimulation.SleepinessPerMinute * 10f).Within(0.01f));
            Assert.That(pet.CurrentMood, Is.Not.EqualTo(PetMood.WantsFood).Or.EqualTo(PetMood.Happy),
                "10분 방치로는 아직 요구가 없어야 한다(느긋한 페이스).");
        }

        [Test]
        public void Needs_NeverExceedBoundsEvenAfterLongNeglect()
        {
            PetSimulation pet = PetSimulation.CreateFresh();
            pet.Tick(60f * 60f * 24f); // 하루 방치해도
            Assert.That(pet.Hunger, Is.LessThanOrEqualTo(PetSimulation.NeedMaximum));
            Assert.That(pet.Dirt, Is.LessThanOrEqualTo(PetSimulation.NeedMaximum));
            Assert.That(pet.Sleepiness, Is.LessThanOrEqualTo(PetSimulation.NeedMaximum));
            // 최악의 경우에도 "슬픔"이 아니라 원하는 것 하나를 알려줄 뿐이다.
            Assert.That(pet.CurrentMood, Is.Not.EqualTo(PetMood.Happy));
        }

        [Test]
        public void Scrubbing_CleansAfterSeveralRubs()
        {
            PetSimulation pet = PetSimulation.Restore(0f, 80f, 0f);
            Assert.That(pet.CurrentMood, Is.EqualTo(PetMood.WantsBath));
            int rubs = 0;
            while (!pet.IsClean && rubs < 20)
            {
                pet.Scrub();
                rubs++;
            }

            Assert.That(pet.IsClean, Is.True);
            Assert.That(rubs, Is.InRange(3, 10), "서너 번 문지르면 깨끗해지는 감각이어야 한다.");
        }

        [Test]
        public void Sleeping_RecoversAndWakesUpByItself()
        {
            PetSimulation pet = PetSimulation.Restore(0f, 0f, 90f);
            Assert.That(pet.CurrentMood, Is.EqualTo(PetMood.Sleepy));
            pet.StartSleep();
            Assert.That(pet.CurrentMood, Is.EqualTo(PetMood.Sleeping));

            pet.Tick(4f);
            Assert.That(pet.Sleepiness, Is.LessThan(90f));

            pet.Tick(30f);
            Assert.That(pet.IsSleeping, Is.False, "다 자면 스스로 일어난다.");
            Assert.That(pet.CurrentMood, Is.EqualTo(PetMood.Happy));
        }

        [Test]
        public void SleepingPausesOtherNeeds()
        {
            PetSimulation pet = PetSimulation.Restore(30f, 30f, 80f);
            pet.StartSleep();
            pet.Tick(2f);
            Assert.That(pet.Hunger, Is.EqualTo(30f).Within(0.01f));
            Assert.That(pet.Dirt, Is.EqualTo(30f).Within(0.01f));
        }

        [Test]
        public void Mood_ShowsOnlyTheStrongestNeed()
        {
            PetSimulation pet = PetSimulation.Restore(70f, 85f, 65f);
            Assert.That(pet.CurrentMood, Is.EqualTo(PetMood.WantsBath),
                "동시에 여러 요구를 하지 않고 가장 급한 것 하나만 보여준다.");
        }
    }
}
