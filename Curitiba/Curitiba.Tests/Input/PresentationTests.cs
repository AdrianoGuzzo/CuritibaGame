using Curitiba.Core.Inputs;
using Curitiba.ScreenManagers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Curitiba.Tests.Input
{
    /// <summary>
    /// Resolution independence: fitting the fixed 800x480 virtual screen into whatever backbuffer
    /// the player has, and mapping a click back the other way.
    /// </summary>
    /// <remarks>
    /// This is pure arithmetic over MonoGame value types, so it runs with no graphics device and no
    /// window — the device is only ever the source of the two backbuffer numbers.
    /// </remarks>
    public class PresentationTests
    {
        private static readonly Vector2 BaseScreenSize = new Vector2(800f, 480f);

        private static PresentationLayout Layout(int width, int height) =>
            ScreenManager.ComputePresentation(BaseScreenSize, width, height);

        // ---------------------------------------------------------------- scaling

        [Fact]
        public void TheNativeResolution_ShouldNeedNoScalingAndNoBars()
        {
            PresentationLayout layout = Layout(800, 480);

            Assert.Equal(1f, layout.ScalingFactor, 4);
            Assert.Equal(0, layout.PresentationViewport.X);
            Assert.Equal(0, layout.PresentationViewport.Y);
            Assert.Equal(800, layout.PresentationViewport.Width);
            Assert.Equal(480, layout.PresentationViewport.Height);
        }

        [Theory]
        [InlineData(1600, 960, 2f)]
        [InlineData(2400, 1440, 3f)]
        [InlineData(400, 240, 0.5f)]
        public void AnExactMultiple_ShouldScaleUniformlyWithNoBars(int width, int height, float scale)
        {
            PresentationLayout layout = Layout(width, height);

            Assert.Equal(scale, layout.ScalingFactor, 4);
            Assert.Equal(0, layout.PresentationViewport.X);
            Assert.Equal(0, layout.PresentationViewport.Y);
        }

        [Theory]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void AWidescreenBackbuffer_ShouldPillarboxNotStretch(int width, int height)
        {
            // 16:9 is wider than the 5:3 virtual screen, so height is the binding axis.
            PresentationLayout layout = Layout(width, height);

            Assert.Equal(height / BaseScreenSize.Y, layout.ScalingFactor, 4);
            Assert.Equal(height, layout.PresentationViewport.Height);
            Assert.True(layout.PresentationViewport.X > 0, "there should be bars left and right");
            Assert.Equal(0, layout.PresentationViewport.Y);
        }

        [Fact]
        public void ATallBackbuffer_ShouldLetterboxInstead()
        {
            // Narrower than 5:3, so width binds and the bars go top and bottom.
            PresentationLayout layout = Layout(800, 1200);

            Assert.Equal(1f, layout.ScalingFactor, 4);
            Assert.Equal(0, layout.PresentationViewport.X);
            Assert.True(layout.PresentationViewport.Y > 0, "there should be bars top and bottom");
        }

        [Theory]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        [InlineData(800, 1200)]
        public void TheViewport_ShouldStayCentred(int width, int height)
        {
            PresentationLayout layout = Layout(width, height);
            Viewport viewport = layout.PresentationViewport;

            Assert.Equal(width - viewport.Width, viewport.X * 2);
            Assert.Equal(height - viewport.Height, viewport.Y * 2);
        }

        [Theory]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void TheViewport_ShouldKeepTheVirtualAspectRatio(int width, int height)
        {
            PresentationLayout layout = Layout(width, height);
            Viewport viewport = layout.PresentationViewport;

            float virtualAspect = BaseScreenSize.X / BaseScreenSize.Y;
            Assert.Equal(virtualAspect, viewport.Width / (float)viewport.Height, 2);
        }

        [Theory]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void TheViewport_ShouldFitInsideTheBackbuffer(int width, int height)
        {
            Viewport viewport = Layout(width, height).PresentationViewport;

            Assert.True(viewport.X + viewport.Width <= width);
            Assert.True(viewport.Y + viewport.Height <= height);
        }

        // ---------------------------------------------------------------- virtual to screen

        [Theory]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void TheDrawTransform_ShouldScaleWithoutTranslating(int width, int height)
        {
            // The bars come from the viewport, not the matrix — the matrix is scale only.
            PresentationLayout layout = Layout(width, height);

            Assert.Equal(layout.ScalingFactor, layout.GlobalTransformation.M11, 4);
            Assert.Equal(layout.ScalingFactor, layout.GlobalTransformation.M22, 4);
            Assert.Equal(Vector3.Zero, layout.GlobalTransformation.Translation);
        }

        // ---------------------------------------------------------------- screen back to virtual

        [Theory]
        [InlineData(800, 480)]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void TheTopLeftOfTheGameArea_ShouldMapToTheVirtualOrigin(int width, int height)
        {
            PresentationLayout layout = Layout(width, height);
            Viewport viewport = layout.PresentationViewport;

            Vector2 virtualPoint = Vector2.Transform(
                new Vector2(viewport.X, viewport.Y), layout.InputTransformation);

            Assert.Equal(0f, virtualPoint.X, 1);
            Assert.Equal(0f, virtualPoint.Y, 1);
        }

        [Theory]
        [InlineData(800, 480)]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void TheBottomRightOfTheGameArea_ShouldMapToTheVirtualExtent(int width, int height)
        {
            PresentationLayout layout = Layout(width, height);
            Viewport viewport = layout.PresentationViewport;

            Vector2 virtualPoint = Vector2.Transform(
                new Vector2(viewport.X + viewport.Width, viewport.Y + viewport.Height),
                layout.InputTransformation);

            Assert.Equal(BaseScreenSize.X, virtualPoint.X, 0);
            Assert.Equal(BaseScreenSize.Y, virtualPoint.Y, 0);
        }

        [Theory]
        [InlineData(800, 480)]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void TheCentreOfTheGameArea_ShouldMapToTheVirtualCentre(int width, int height)
        {
            PresentationLayout layout = Layout(width, height);
            Viewport viewport = layout.PresentationViewport;

            Vector2 virtualPoint = Vector2.Transform(
                new Vector2(viewport.X + viewport.Width / 2f, viewport.Y + viewport.Height / 2f),
                layout.InputTransformation);

            Assert.Equal(BaseScreenSize.X / 2f, virtualPoint.X, 0);
            Assert.Equal(BaseScreenSize.Y / 2f, virtualPoint.Y, 0);
        }

        [Theory]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void APointInTheLetterboxBar_ShouldMapOutsideTheVirtualScreen(int width, int height)
        {
            PresentationLayout layout = Layout(width, height);

            Vector2 virtualPoint = Vector2.Transform(new Vector2(0f, height / 2f), layout.InputTransformation);

            Assert.True(virtualPoint.X < 0f, "a click on the bar is not a click on the game");
        }

        [Theory]
        [InlineData(1280, 720, 200f, 150f)]
        [InlineData(1920, 1080, 400f, 240f)]
        [InlineData(2560, 1440, 799f, 479f)]
        public void VirtualToScreenAndBack_ShouldRoundTrip(int width, int height, float vx, float vy)
        {
            PresentationLayout layout = Layout(width, height);
            Viewport viewport = layout.PresentationViewport;

            // Virtual to screen is the scale plus the letterbox offset the viewport carries.
            var screenPoint = new Vector2(vx * layout.ScalingFactor + viewport.X,
                                          vy * layout.ScalingFactor + viewport.Y);
            Vector2 back = Vector2.Transform(screenPoint, layout.InputTransformation);

            Assert.Equal(vx, back.X, 1);
            Assert.Equal(vy, back.Y, 1);
        }
    }

    /// <summary>How <see cref="InputState"/> unprojects a cursor position.</summary>
    public class InputTransformationTests
    {
        [Fact]
        public void AFreshInputState_ShouldMapEveryCursorPositionToTheOrigin()
        {
            // Recorded, not fixed: the transform field starts as default(Matrix) — all zeros, not
            // the identity — so until ScalePresentationArea has run once, every cursor position
            // collapses to (0,0). In the game that always happens during the first LoadContent.
            var input = new InputState();

            Assert.Equal(Vector2.Zero, input.TransformCursorLocation(new Vector2(640f, 360f)));
        }

        [Theory]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(2560, 1440)]
        public void AConfiguredInputState_ShouldUnprojectAClickIntoVirtualSpace(int width, int height)
        {
            var input = new InputState();
            PresentationLayout layout = ScreenManager.ComputePresentation(new Vector2(800f, 480f), width, height);
            input.UpdateInputTransformation(layout.InputTransformation);
            Viewport viewport = layout.PresentationViewport;

            Vector2 virtualPoint = input.TransformCursorLocation(
                new Vector2(viewport.X + viewport.Width / 2f, viewport.Y + viewport.Height / 2f));

            Assert.Equal(400f, virtualPoint.X, 0);
            Assert.Equal(240f, virtualPoint.Y, 0);
        }
    }
}
