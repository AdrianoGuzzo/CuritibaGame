using Microsoft.Xna.Framework;

namespace Curitiba.Core.Audio
{
    /// <summary>Where the arena track is in its life.</summary>
    internal enum ArenaMusicState
    {
        /// <summary>Nothing has been started yet.</summary>
        Idle,

        /// <summary>Playing, ramping up from silence.</summary>
        FadingIn,

        /// <summary>Playing behind the fight, at the background volume.</summary>
        Playing,

        /// <summary>Playing under the pause menu, quieter.</summary>
        Ducked,

        /// <summary>Playing, ramping down towards the hand-over to the next screen.</summary>
        FadingOut,

        /// <summary>Silent for good. Nothing reaches the player from here.</summary>
        Stopped
    }

    /// <summary>
    /// Decides, frame by frame, what the beat 'em up's music channel should be doing: fade in when
    /// the stage starts, sit behind the action, duck under the pause menu, and fade out as the
    /// screen hands over.
    /// </summary>
    /// <remarks>
    /// Split out of <c>BeatEmUpScreen</c> so the rule is a plain state machine over a float clock,
    /// with no <c>Song</c>, no <c>MediaPlayer</c> and no screen attached to it.
    /// <para>
    /// The guarantee that matters is the ceiling: this is the stage's only music, meant to stay
    /// under the fight rather than compete with it, so no path here — not the ramp, not unpausing,
    /// not the way out — may put the track above <see cref="BackgroundVolume"/>.
    /// </para>
    /// </remarks>
    internal sealed class ArenaMusicPolicy
    {
        /// <summary>The stage theme, as the content pipeline names it.</summary>
        public const string Track = "Music/RubberBassRiot";

        /// <summary>
        /// Where the track sits while the stage plays. Well under full volume on purpose: it is
        /// background, and it is the only music the arena has.
        /// </summary>
        public const float BackgroundVolume = 0.30f;

        /// <summary>Where the track drops to while the pause menu is up.</summary>
        public const float DuckedVolume = 0.12f;

        private readonly float fadeInSeconds;
        private readonly float fadeOutSeconds;

        private ArenaMusicState state = ArenaMusicState.Idle;
        private float elapsed;
        private float volume;
        private float fadeOutFrom = BackgroundVolume;
        private bool started;

        /// <param name="fadeInSeconds">Ramp up when the stage starts. Zero or less starts at <see cref="BackgroundVolume"/>.</param>
        /// <param name="fadeOutSeconds">Ramp down once <see cref="BeginExit"/> is called, normally the screen's transition off.</param>
        public ArenaMusicPolicy(float fadeInSeconds, float fadeOutSeconds)
        {
            this.fadeInSeconds = fadeInSeconds;
            this.fadeOutSeconds = fadeOutSeconds;
        }

        /// <summary>Where the track is in its life.</summary>
        public ArenaMusicState State => state;

        /// <summary>
        /// Starts the fade-out, from whatever volume is playing right now. Idempotent: a second call
        /// does not restart it, so a stage that completes and is quit out of cannot stretch the fade
        /// past the hand-over.
        /// </summary>
        public void BeginExit()
        {
            if (state == ArenaMusicState.FadingOut || state == ArenaMusicState.Stopped)
                return;

            if (state == ArenaMusicState.Idle)
            {
                // Leaving before a single frame ran: there is nothing to fade.
                state = ArenaMusicState.Stopped;
                return;
            }

            // From the current level, never from the nominal one: quitting out of the pause menu
            // must not make the track louder on its way out.
            fadeOutFrom = volume;
            state = ArenaMusicState.FadingOut;
            elapsed = 0f;
        }

        /// <summary>
        /// Advances one frame, issuing volume and playback changes on <paramref name="player"/>.
        /// </summary>
        /// <param name="paused">
        /// Whether a screen is covering the stage — the pause menu, or the loading screen on the way
        /// out. Only <see cref="ArenaMusicState.Playing"/> and <see cref="ArenaMusicState.Ducked"/>
        /// answer to it: a fade already under way has somewhere to be.
        /// </param>
        public void Update(float elapsedSeconds, IMusicPlayer player, bool paused)
        {
            if (player == null)
                return;

            switch (state)
            {
                case ArenaMusicState.Idle:
                    Start(player);
                    return;

                case ArenaMusicState.FadingIn:
                    Advance(elapsedSeconds);
                    if (elapsed >= fadeInSeconds)
                    {
                        SetVolume(player, BackgroundVolume);
                        state = ArenaMusicState.Playing;
                        return;
                    }

                    SetVolume(player, BackgroundVolume * MathHelper.Clamp(elapsed / fadeInSeconds, 0f, 1f));
                    return;

                case ArenaMusicState.Playing:
                    if (paused)
                    {
                        SetVolume(player, DuckedVolume);
                        state = ArenaMusicState.Ducked;
                    }

                    // Otherwise steady: writing the same volume every frame would push a value at
                    // the audio backend for nothing.
                    return;

                case ArenaMusicState.Ducked:
                    if (!paused)
                    {
                        SetVolume(player, BackgroundVolume);
                        state = ArenaMusicState.Playing;
                    }

                    return;

                case ArenaMusicState.FadingOut:
                    Advance(elapsedSeconds);
                    if (elapsed >= fadeOutSeconds)
                    {
                        Silence(player);
                        return;
                    }

                    SetVolume(player, fadeOutFrom * MathHelper.Clamp(1f - elapsed / fadeOutSeconds, 0f, 1f));
                    return;

                default:
                    // Stopped is final.
                    return;
            }
        }

        /// <summary>
        /// Silences the track now, whatever state it is in. Idempotent, and a no-op if the track was
        /// never started — <c>UnloadContent</c> runs even for a stage that was exited before its
        /// first <see cref="Update"/>.
        /// </summary>
        public void Stop(IMusicPlayer player)
        {
            if (state == ArenaMusicState.Stopped)
                return;

            if (!started)
            {
                state = ArenaMusicState.Stopped;
                return;
            }

            Silence(player);
        }

        /// <summary>
        /// Sets the starting volume <em>before</em> starting the track, so it is never audible at the
        /// wrong level for a frame — which right after the menu's fade-out would be a stab of
        /// full-volume music. The frame that starts the track does not advance the ramp.
        /// </summary>
        private void Start(IMusicPlayer player)
        {
            bool instant = fadeInSeconds <= 0f;

            SetVolume(player, instant ? BackgroundVolume : 0f);
            player.Play(Track, true);

            started = true;
            state = instant ? ArenaMusicState.Playing : ArenaMusicState.FadingIn;
        }

        private void Silence(IMusicPlayer player)
        {
            if (player != null)
            {
                SetVolume(player, 0f);
                player.Stop();
            }

            state = ArenaMusicState.Stopped;
        }

        /// <summary>Writes a volume through, remembering it so a fade-out knows where to start.</summary>
        private void SetVolume(IMusicPlayer player, float value)
        {
            volume = value;
            player.Volume = value;
        }

        /// <summary>A frozen or rewound clock must never move the ramp backwards.</summary>
        private void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds > 0f)
                elapsed += elapsedSeconds;
        }
    }
}
