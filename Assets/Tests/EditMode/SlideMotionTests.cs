using NUnit.Framework;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The player's slide (GL872): it starts on landing with Crouch still held, keeps the speed carried in, and chained
    /// slides downhill keep or gain speed up to the top speed.
    /// </summary>
    public class SlideMotionTests
    {
        // The player's defaults: walk 4, sprint 7, crouch 2, top 17.5.
        private static readonly SlideMotion.Tuning T = new SlideMotion.Tuning
        {
            WalkSpeed = 4f,
            SprintSpeed = 7f,
            CrouchSpeed = 2f,
            Boost = 1.2f,
            Drag = 3f,
            Gravity = 9.81f,
            SteepestRise = 0.2f,
            MaxDistance = 8f,
            TopSpeed = 17.5f,
        };

        [Test]
        public void Starts_WhenCrouchIsStillHeldOnLanding_FromTheCarriedSpeed()
        {
            // Landing from a jump with C held: Sprint held, the run-up still carried, the capsule already crouched.
            Assert.IsTrue(SlideMotion.Starts(T, crouchHeld: true, spent: false, nearGround: true, sprinting: true, carriedSpeed: 8.4f, rise: -0.4f));
        }

        [Test]
        public void Starts_WithoutSprint_WhenCarryingASprintsSpeed()
        {
            Assert.IsTrue(SlideMotion.Starts(T, true, false, true, sprinting: false, carriedSpeed: 9f, rise: 0f));
            Assert.IsFalse(SlideMotion.Starts(T, true, false, true, sprinting: false, carriedSpeed: 5f, rise: 0f));
        }

        [Test]
        public void DoesNotStart_FromACrouchWalk_InTheAir_UpASteepRise_OrWhenSpent()
        {
            Assert.IsFalse(SlideMotion.Starts(T, true, false, true, true, carriedSpeed: 2f, rise: 0f), "crouch walk");
            Assert.IsFalse(SlideMotion.Starts(T, true, false, nearGround: false, true, 8f, 0f), "in the air");
            Assert.IsFalse(SlideMotion.Starts(T, true, false, true, true, 8f, rise: 0.3f), "steep rise");
            Assert.IsFalse(SlideMotion.Starts(T, true, spent: true, true, true, 8f, 0f), "spent");
            Assert.IsFalse(SlideMotion.Starts(T, crouchHeld: false, false, true, true, 8f, 0f), "not held");
        }

        [Test]
        public void StartSpeed_BoostsASprint_KeepsAFasterRunUp_AndStopsAtTheTop()
        {
            Assert.AreEqual(8.4f, SlideMotion.StartSpeed(T, 7f), 1e-4f);
            Assert.AreEqual(12f, SlideMotion.StartSpeed(T, 12f), 1e-4f);
            Assert.AreEqual(17.5f, SlideMotion.StartSpeed(T, 30f), 1e-4f);
        }

        [Test]
        public void ChainedSlidesDownASlope_KeepOrGainSpeed_UpToTheTop()
        {
            const float rise = -0.5f; // about 27 degrees down
            const float dt = 1f / 60f;
            float carried = 7f; // a sprint
            float previousStart = 0f;
            for (int slide = 0; slide < 6; slide++)
            {
                float start = SlideMotion.StartSpeed(T, carried);
                Assert.GreaterOrEqual(start, previousStart, $"slide {slide} starts slower than the last");
                float speed = start, distance = 0f;
                for (int frame = 0; frame < 60; frame++)
                {
                    speed = SlideMotion.Step(T, speed, start, rise, dt);
                    distance += SlideMotion.CountedDistance(speed, rise, dt);
                    Assert.IsFalse(SlideMotion.Ends(T, speed, distance, rise), $"slide {slide} ended downhill at frame {frame}");
                }
                Assert.LessOrEqual(speed, T.TopSpeed + 1e-4f);
                // A jump out keeps the speed as the run-up for the next slide.
                carried = speed;
                previousStart = start;
            }
            Assert.AreEqual(T.TopSpeed, carried, 1e-3f);
        }

        [Test]
        public void OnLevelGround_ASlideSlows_AndEndsAtItsDistance()
        {
            float start = SlideMotion.StartSpeed(T, 7f), speed = start, distance = 0f;
            int frames = 0;
            while (!SlideMotion.Ends(T, speed, distance, 0f) && frames < 1000)
            {
                speed = SlideMotion.Step(T, speed, start, 0f, 1f / 60f);
                distance += SlideMotion.CountedDistance(speed, 0f, 1f / 60f);
                frames++;
            }
            Assert.Less(speed, start);
            Assert.GreaterOrEqual(distance, T.MaxDistance - 0.2f);
        }
    }
}
