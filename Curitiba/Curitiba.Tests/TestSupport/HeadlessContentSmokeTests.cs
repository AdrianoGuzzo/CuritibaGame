using Curitiba.Core.BeatEmUp;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// Guards the assumption the whole fighter/arena suite rests on: that the game's own
    /// missing-art fallback lets combatants be built with no graphics device.
    /// </summary>
    /// <remarks>
    /// If these ever fail, the fallback has been narrowed and the suite needs a seam of its own
    /// (an animator interface with a no-op implementation) rather than the content manager.
    /// </remarks>
    public class HeadlessContentSmokeTests
    {
        [Fact]
        public void MissingAsset_ShouldThrowContentLoadException_NotSomethingElse()
        {
            // Arrange
            ContentManager content = HeadlessContent.Create();

            // Act & Assert — the graphics device is never consulted, because the stream misses first.
            Assert.Throws<ContentLoadException>(() => content.Load<Texture2D>("Sprites/Sofia/Idle"));
        }

        [Fact]
        public void SofiaPlayer_ShouldBeConstructible_WithoutGraphics()
        {
            // Arrange & Act
            var sofia = new SofiaPlayer(HeadlessContent.Create(), null);

            // Assert
            Assert.Equal(FighterState.Idle, sofia.State);
            Assert.Equal(100, sofia.Health);
            Assert.Null(sofia.Portrait);
        }

        [Fact]
        public void PiaLocoEnemy_ShouldBeConstructible_WithoutGraphics()
        {
            // Arrange
            ContentManager content = HeadlessContent.Create();
            var sofia = new SofiaPlayer(content, null);

            // Act
            PiaLocoEnemy enemy = Enemies.Create(content, sofia);

            // Assert
            Assert.Equal(FighterState.Idle, enemy.State);
            Assert.Equal(30, enemy.Health);
        }

        [Fact]
        public void Fighter_ShouldRunItsStateMachine_WithoutGraphics()
        {
            // Arrange
            var sofia = new SofiaPlayer(HeadlessContent.Create(), null);

            // Act — a buffered attack must actually start a swing on the next update.
            sofia.RequestAttack();
            sofia.Update(Frames.Step());

            // Assert
            Assert.Equal(FighterState.Attack, sofia.State);
        }
    }
}
