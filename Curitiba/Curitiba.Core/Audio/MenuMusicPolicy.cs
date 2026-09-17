using Microsoft.Xna.Framework;

namespace Curitiba.Core.Audio
{
    /// <summary>Where the menu track is in its life.</summary>
    internal enum MenuMusicState
    {
        /// <summary>Nothing has been started yet.</summary>
        Idle,

        /// <summary>Playing, ramping up from silence.</summary>
        FadingIn,

        /// <summary>Playing at full volume.</summary>
        Playing,

        /// <summary>Playing, ramping down towards the hand-over to the arena.</summary>
        FadingOut,

        /// <summary>Silent for good. Nothing reaches the player from here.</summary>
        Stopped
    }

    /// <summary>
    /// Decides, frame by frame, what the menu's music channel should be doing: fade in on arrival,
    /// hold while the player browses, fade out over the "Play" cinematic, and go silent on the frame
    /// the cinematic ends.
    /// </summary>
    /// <remarks>
    /// Split out of <c>MainMenuScreen</c> so the rule is a plain state machine over a float clock,
    /// with no <c>Song</c>, no <c>MediaPlayer</c> and no screen attached to it.
    /// <para>
    /// The guarantee that matters is the last one: the frame the fade-out completes is the frame
    /// <c>MainMenuScreen</c> hands over to <c>LoadingScreen</c>, so stopping late would leak menu
    /// music into a beat 'em up arena that is meant to be silent.
    /// </para>
    /// </remarks>
    internal sealed class MenuMusicPolicy
    {
        /// <summary>The menu theme, as the content pipeline names it.</summary>
        public const string Track = "Music/SunlightOnTheShrubs";

        /// <summary>Seconds the track takes to ramp up when the menu appears.</summary>
        public const float DefaultFadeInSeconds = 1.0f;

        private readonly float fadeInSeconds;
        private readonly float fadeOutSeconds;

        private MenuMusicState state = MenuMusicState.Idle;
        private float elapsed;
        private bool started;

        /// <param name="fadeInSeconds">Ramp up when the menu appears. Zero or less starts at full volume.</param>
        /// <param name="fadeOutSeconds">Ramp down once <see cref="BeginExit"/> is called, normally the cinematic's length.</param>
        public MenuMusicPolicy(float fadeInSeconds, float fadeOutSeconds)
        {
            this.fadeInSeconds = fadeInSeconds;
            this.fadeOutSeconds = fadeOutSeconds;
        }

        /// <summary>Where the track is in its life.</summary>
        public MenuMusicState State => state;

        /// <summary>
        /// Starts the fade-out. Idempotent: a second call does not restart it, so holding the button
        /// or re-entering the cinematic cannot stretch the fade past the hand-over.
        /// </summary>
        public void BeginExit()
        {
            if (state == MenuMusicState.FadingOut || state == MenuMusicState.Stopped)
                return;

            if (state == MenuMusicState.Idle)
            {
                // Leaving before a single frame ran: there is nothing to fade.
                state = MenuMusicState.Stopped;
                return;
            }

            state = MenuMusicState.FadingOut;
            elapsed = 0f;
        }

        /// <summary>
        /// Advances one frame, issuing volume and playback changes on <paramref name="player"/>.
        /// </summary>
        public void Update(float elapsedSeconds, IMusicPlayer player)
        {
            if (player == null)
                return;

            switch (state)
            {
                case MenuMusicState.Idle:
                    Start(player);
                    return;

                case MenuMusicState.FadingIn:
                    Advance(elapsedSeconds);
                    if (elapsed >= fadeInSeconds)
                    {
                        player.Volume = 1f;
                        state = MenuMusicState.Playing;
                        return;
                    }

                    player.Volume = MathHelper.Clamp(elapsed / fadeInSeconds, 0f, 1f);
                    return;

                case MenuMusicState.FadingOut:
                    Advance(elapsedSeconds);
                    if (elapsed >= fadeOutSeconds)
                    {
                        Silence(player);
                        return;
                    }

                    player.Volume = MathHelper.Clamp(1f - elapsed / fadeOutSeconds, 0f, 1f);
                    return;

                default:
                    // Playing and Stopped are both steady: writing the same volume every frame would
                    // push a value at the audio backend for nothing.
                    return;
            }
        }

        /// <summary>
        /// Silences the track now, whatever state it is in. Idempotent, and a no-op if the track was
        /// never started — <c>UnloadContent</c> runs even for a menu that was exited before its first
        /// <see cref="Update"/>.
        /// </summary>
        public void Stop(IMusicPlayer player)
        {
            if (state == MenuMusicState.Stopped)
                return;

            if (!started)
            {
                state = MenuMusicState.Stopped;
                return;
            }

            Silence(player);
        }

        /// <summary>
        /// Sets the starting volume <em>before</em> starting the track, so it is never audible at the
        /// wrong level for a frame — which is exactly what re-entering the menu after a fade-out
        /// would otherwise sound like. The frame that starts the track does not advance the ramp.
        /// </summary>
        private void Start(IMusicPlayer player)
        {
            bool instant = fadeInSeconds <= 0f;

            player.Volume = instant ? 1f : 0f;
            player.Play(Track, true);

            started = true;
            state = instant ? MenuMusicState.Playing : MenuMusicState.FadingIn;
        }

        private void Silence(IMusicPlayer player)
        {
            if (player != null)
            {
                player.Volume = 0f;
                player.Stop();
            }

            state = MenuMusicState.Stopped;
        }

        /// <summary>A frozen or rewound clock must never move the ramp backwards.</summary>
        private void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds > 0f)
                elapsed += elapsedSeconds;
        }
    }
}
