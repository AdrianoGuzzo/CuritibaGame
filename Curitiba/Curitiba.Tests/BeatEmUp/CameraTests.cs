using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The scrolling camera, and with it the classic beat 'em up "lock": while a wave is alive the
    /// arena pins <c>MaxAdvanceX</c> so the player cannot walk past the fight.
    /// </summary>
    public class CameraTests
    {
        private const float ViewWidth = 800f;
        private const float WorldWidth = 2400f;

        private static Camera2D NewCamera(float worldWidth = WorldWidth) => new Camera2D(ViewWidth, worldWidth);

        [Fact]
        public void NewCamera_ShouldAllowAdvancingToTheEndOfTheWorld()
        {
            var camera = NewCamera();

            Assert.Equal(WorldWidth - ViewWidth, camera.MaxAdvanceX);
            Assert.Equal(0f, camera.X);
        }

        [Fact]
        public void Snap_ShouldCentreTheFocus()
        {
            var camera = NewCamera();

            camera.Snap(new Vector2(1200f, 0f));

            // 1200 - 800/2 = 800.
            Assert.Equal(800f, camera.X);
        }

        [Fact]
        public void Snap_ShouldNotScrollLeftOfTheWorld()
        {
            var camera = NewCamera();

            camera.Snap(new Vector2(50f, 0f));

            Assert.Equal(0f, camera.X);
        }

        [Fact]
        public void Snap_ShouldNotScrollPastTheEndOfTheWorld()
        {
            var camera = NewCamera();

            camera.Snap(new Vector2(WorldWidth + 500f, 0f));

            Assert.Equal(WorldWidth - ViewWidth, camera.X);
        }

        [Fact]
        public void Left_And_Right_ShouldSpanTheView()
        {
            var camera = NewCamera();
            camera.Snap(new Vector2(1200f, 0f));

            Assert.Equal(camera.X, camera.Left);
            Assert.Equal(camera.X + ViewWidth, camera.Right);
        }

        [Fact]
        public void Follow_ShouldEaseTowardTheFocus_WithoutOvershooting()
        {
            var camera = NewCamera();

            camera.Follow(new Vector2(1200f, 0f), Frames.DefaultStep);

            Assert.True(camera.X > 0f, "camera should have started moving");
            Assert.True(camera.X < 800f, "a single eased frame must not arrive already");
        }

        [Fact]
        public void Follow_ShouldConvergeOnTheFocus()
        {
            var camera = NewCamera();

            for (int i = 0; i < 120; i++)
                camera.Follow(new Vector2(1200f, 0f), Frames.DefaultStep);

            Assert.Equal(800f, camera.X);
        }

        [Fact]
        public void Follow_ShouldSnap_WhenWithinHalfAPixel()
        {
            var camera = NewCamera();
            camera.Snap(new Vector2(1200f, 0f));

            // A focus 0.4px away is inside the snap threshold, so it lands exactly rather than easing.
            camera.Follow(new Vector2(1200.4f, 0f), Frames.DefaultStep);

            Assert.Equal(800.4f, camera.X);
        }

        [Fact]
        public void Follow_ShouldNotScrollPastTheLock_WhileTheWaveIsAlive()
        {
            var camera = NewCamera();
            camera.MaxAdvanceX = 400f;

            // The player keeps walking right, well past the lock point.
            for (int i = 0; i < 240; i++)
                camera.Follow(new Vector2(2000f, 0f), Frames.DefaultStep);

            Assert.Equal(400f, camera.X);
        }

        [Fact]
        public void Follow_ShouldAdvancePastTheOldLock_OnceTheWaveIsCleared()
        {
            var camera = NewCamera();
            camera.MaxAdvanceX = 400f;
            for (int i = 0; i < 240; i++)
                camera.Follow(new Vector2(2000f, 0f), Frames.DefaultStep);
            Assert.Equal(400f, camera.X);

            // Clearing the wave is exactly this: the arena raises the bound.
            camera.MaxAdvanceX = WorldWidth - ViewWidth;
            for (int i = 0; i < 240; i++)
                camera.Follow(new Vector2(2000f, 0f), Frames.DefaultStep);

            Assert.Equal(1600f, camera.X);
        }

        [Fact]
        public void Follow_ShouldStillTrackBackwards_WhileLocked()
        {
            var camera = NewCamera();
            camera.Snap(new Vector2(1200f, 0f));
            camera.MaxAdvanceX = 400f;

            for (int i = 0; i < 240; i++)
                camera.Follow(new Vector2(300f, 0f), Frames.DefaultStep);

            // The lock is an upper bound only; walking left still pans.
            Assert.Equal(0f, camera.X);
        }

        [Theory]
        [InlineData(-500f)]
        [InlineData(0f)]
        public void Follow_ShouldClampToZero_WhenTheLockIsAtOrBelowTheOrigin(float lockX)
        {
            var camera = NewCamera();
            camera.MaxAdvanceX = lockX;

            for (int i = 0; i < 60; i++)
                camera.Follow(new Vector2(2000f, 0f), Frames.DefaultStep);

            Assert.Equal(0f, camera.X);
        }

        [Fact]
        public void Follow_ShouldStillRespectTheWorld_WhenTheLockIsBeyondIt()
        {
            var camera = NewCamera();
            camera.MaxAdvanceX = WorldWidth * 10f;

            for (int i = 0; i < 240; i++)
                camera.Follow(new Vector2(WorldWidth * 5f, 0f), Frames.DefaultStep);

            Assert.Equal(WorldWidth - ViewWidth, camera.X);
        }

        [Fact]
        public void Camera_ShouldStayAtOrigin_WhenTheWorldIsNarrowerThanTheView()
        {
            var camera = NewCamera(worldWidth: 600f);

            camera.Snap(new Vector2(500f, 0f));

            Assert.Equal(0f, camera.X);
        }

        [Fact]
        public void SetX_ShouldIgnoreTheLock_BecauseItIsTheEditorFreePan()
        {
            var camera = NewCamera();
            camera.MaxAdvanceX = 100f;

            camera.SetX(1500f);

            Assert.Equal(1500f, camera.X);
        }

        [Fact]
        public void SetX_ShouldStillClampToTheWorld()
        {
            var camera = NewCamera();

            camera.SetX(99999f);

            Assert.Equal(WorldWidth - ViewWidth, camera.X);
        }

        [Fact]
        public void GetTransform_ShouldTranslateByRoundedX_ToAvoidJitter()
        {
            var camera = NewCamera();
            camera.SetX(123.6f);

            Matrix transform = camera.GetTransform();

            Assert.Equal(-124f, transform.Translation.X);
            Assert.Equal(0f, transform.Translation.Y);
        }
    }
}
