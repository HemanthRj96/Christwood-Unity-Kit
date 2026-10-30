using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace UnityWorkshop.Tests
{
    /// <summary>
    /// Side-scroller template: plane lock (no depth drift), repeatable jumps, camera bounds and calm vertical
    /// follow, keyboard movement through the real input actions, and the shared mechanics on Cliff Run.
    /// </summary>
    public sealed class SideScrollerTests : WorkshopPlayTestBase
    {
        const string SideScrollerExamplePath = "Assets/WorkshopKit/Examples/SideScroller_Example.unity";

        SideScrollerCameraRig Rig => (SideScrollerCameraRig)m_Context.CameraRig;
        Transform PlayerTransform => m_Context.Player.transform;

        [UnityTest]
        public IEnumerator StartsOnThePlaneFacingRightWithTheMouseFree()
        {
            yield return StartScene(SideScrollerExamplePath);
            Assert.That(PlayerTransform.position.z, Is.EqualTo(Rig.PlaneDepth).Within(0.0001f));
            Assert.That(Mathf.DeltaAngle(PlayerTransform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(0.5f));
            Assert.That(m_Context.CursorShouldLock, Is.False, "Side-scrollers need no mouse.");
            Assert.That(Rig.Focus.x, Is.InRange(Rig.BoundsMin.x, Rig.BoundsMax.x));
            Assert.That(Rig.Focus.y, Is.InRange(Rig.BoundsMin.y, Rig.BoundsMax.y));
            Assert.That(m_Context.Player.Motor.IsDepthLocked, Is.True);
        }

        [UnityTest]
        public IEnumerator RunningAndJumpingNeverLeaveThePlane()
        {
            yield return StartScene(SideScrollerExamplePath);
            var startX = PlayerTransform.position.x;
            var worstDrift = 0f;
            Keys.Press(Key.D);
            var until = Time.time + 1f;
            var nextJump = Time.time + 0.1f;
            while (Time.time < until)
            {
                if (Time.time >= nextJump)
                {
                    Keys.Press(Key.D, Key.Space);
                    nextJump = Time.time + 0.45f;
                }
                else
                {
                    Keys.Press(Key.D);
                }
                yield return null;
                worstDrift = Mathf.Max(worstDrift, Mathf.Abs(PlayerTransform.position.z - Rig.PlaneDepth));
            }
            Keys.ReleaseAll();
            Assert.That(PlayerTransform.position.x - startX, Is.GreaterThan(2.5f), "D runs right.");
            Assert.That(worstDrift, Is.LessThan(0.001f), "No depth drift while running and jumping.");

            // A collision or teleport off the plane is pulled straight back.
            m_Context.Player.Motor.UnlockDepth();
            m_Context.Player.PlaceAt(new Pose(new Vector3(-6f, 0.05f, 0.8f), Quaternion.Euler(0f, 90f, 0f)));
            yield return null;
            yield return null;
            Assert.That(PlayerTransform.position.z, Is.EqualTo(Rig.PlaneDepth).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator UpAndDownKeysDoNothingAndFacingFollowsDirection()
        {
            yield return StartScene(SideScrollerExamplePath);
            var start = PlayerTransform.position;
            yield return Hold(0.4f, Key.W);
            yield return Hold(0.4f, Key.S);
            Assert.That(Vector3.Distance(PlayerTransform.position, start), Is.LessThan(0.02f), "W and S do not move a side-scroller.");

            yield return Hold(0.3f, Key.LeftArrow);
            Assert.That(PlayerTransform.position.x, Is.LessThan(start.x - 0.3f), "Left arrow moves left.");
            Assert.That(Mathf.DeltaAngle(PlayerTransform.eulerAngles.y, 270f), Is.EqualTo(0f).Within(5f), "The character faces left.");
            yield return Hold(0.3f, Key.RightArrow);
            Assert.That(Mathf.DeltaAngle(PlayerTransform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(5f), "Then right again.");
        }

        [UnityTest]
        public IEnumerator JumpsAreRepeatable()
        {
            yield return StartScene(SideScrollerExamplePath);
            var x = PlayerTransform.position.x;
            var peaks = new float[5];
            for (var jump = 0; jump < peaks.Length; jump++)
            {
                Keys.Press(Key.Space);
                yield return null;
                Keys.ReleaseAll();
                var until = Time.time + 0.9f;
                while (Time.time < until)
                {
                    peaks[jump] = Mathf.Max(peaks[jump], PlayerTransform.position.y);
                    yield return null;
                }
                Assert.That(m_Context.Player.Motor.IsGrounded, Is.True, $"Landed after jump {jump}.");
            }
            Assert.That(peaks.Max() - peaks.Min(), Is.LessThan(0.05f), "Every jump reaches the same height.");
            Assert.That(peaks.Min(), Is.GreaterThan(1f));
            Assert.That(Mathf.Abs(PlayerTransform.position.x - x), Is.LessThan(0.01f), "Jumping in place lands in place.");
            Assert.That(PlayerTransform.position.z, Is.EqualTo(Rig.PlaneDepth).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator CameraStaysInBoundsAndKeepsSmallJumpsCalm()
        {
            yield return StartScene(SideScrollerExamplePath);
            yield return MovePlayerTo(new Vector3(-9.5f, 0f, 0f));
            yield return WaitSeconds(0.8f);
            Assert.That(Rig.Focus.x, Is.EqualTo(Rig.BoundsMin.x).Within(0.05f), "The camera does not look past the left bound.");

            yield return MovePlayerTo(new Vector3(8f, 0f, 0f));
            yield return WaitSeconds(1.2f);
            Assert.That(Rig.Focus.x, Is.EqualTo(8f).Within(0.1f), "Follows sideways.");
            var calmY = Rig.Focus.y;

            Keys.Press(Key.Space);
            yield return null;
            Keys.ReleaseAll();
            var highest = calmY;
            var until = Time.time + 0.9f;
            while (Time.time < until)
            {
                highest = Mathf.Max(highest, Rig.Focus.y);
                yield return null;
            }
            Assert.That(highest - calmY, Is.LessThan(0.05f), "A normal jump does not move the camera up and down.");

            // Standing high on the floating platform leaves the calm zone, so the camera rises just enough.
            yield return MovePlayerTo(new Vector3(30.5f, 1.8f, 0f));
            yield return WaitSeconds(1.2f);
            var aim = 1.8f + SideScrollerCameraRig.AimHeight;
            Assert.That(Rig.Focus.y, Is.EqualTo(aim - Rig.VerticalCalmZone).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator MovingSpikesStayOnThePlane()
        {
            yield return StartScene(SideScrollerExamplePath);
            var mover = Object.FindObjectsByType<HazardMover>().Single();
            var start = mover.transform.position;
            yield return WaitSeconds(1.5f);
            Assert.That(mover.transform.position.x, Is.Not.EqualTo(start.x).Within(0.1f), "It moves.");
            Assert.That(mover.transform.position.z, Is.EqualTo(Rig.PlaneDepth).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator CliffRunCourseWorks()
        {
            yield return StartScene(SideScrollerExamplePath);
            var gems = Collectibles();
            Assert.That(gems.Length, Is.EqualTo(5));
            foreach (var gem in gems.Where(g => g.transform.position.x < 14f))
                yield return MovePlayerTo(gem.transform.position);
            Assert.That(Session.CollectedCount, Is.EqualTo(2));

            var checkpoint = Object.FindObjectsByType<Checkpoint>().Single();
            yield return MovePlayerTo(checkpoint.transform.position);
            Assert.That(m_Context.ActiveCheckpoint, Is.EqualTo(checkpoint));

            // Missing a jump: falling below the level returns the player to the checkpoint.
            m_Context.Player.PlaceAt(new Pose(new Vector3(32.5f, m_Context.FallHeight - 1f, 0f), Quaternion.identity));
            yield return null;
            yield return null;
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning));
            Assert.That(Vector3.Distance(PlayerTransform.position, checkpoint.RespawnPose.position), Is.LessThan(0.5f));
            Assert.That(Rig.Focus.x, Is.EqualTo(checkpoint.transform.position.x).Within(0.05f), "The camera snaps to the respawn.");
            yield return WaitWhileRespawning();

            foreach (var gem in gems.Where(g => g.transform.position.x >= 14f))
                yield return MovePlayerTo(gem.transform.position);
            Assert.That(Session.CollectedCount, Is.EqualTo(5));
            yield return MovePlayerTo(Object.FindObjectsByType<GoalZone>().Single().transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Won));
        }

        [UnityTest]
        public IEnumerator RestartRestoresTheSideScrollerTenTimes()
        {
            yield return StartScene(SideScrollerExamplePath);
            for (var cycle = 0; cycle < 10; cycle++)
            {
                yield return MovePlayerTo(Collectibles()[cycle % 5].transform.position);
                if (cycle % 3 == 1)
                    m_Context.TogglePause();
                var previous = m_Context;
                previous.RestartGame();
                yield return WaitUntilPlaying(previous);
                Assert.That(Session.CollectedCount, Is.EqualTo(0), $"cycle {cycle}");
                Assert.That(Collectibles().All(c => !c.IsCollected), Is.True);
                Assert.That(PlayerTransform.position.z, Is.EqualTo(Rig.PlaneDepth).Within(0.0001f));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
        }
    }
}
