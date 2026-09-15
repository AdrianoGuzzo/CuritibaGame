using System;

namespace Curitiba.Core.DevTools
{
    /// <summary>
    /// Decides, frame by frame, when a signalled stage-file change should actually rebuild the arena.
    /// A save is not atomic: the editor may still hold the file, or the JSON may be momentarily (or
    /// permanently) malformed. So a change arms a small retry budget that is spent one frame at a
    /// time, and the rebuild happens only on the frame the load succeeds — an invalid file simply
    /// exhausts the budget and leaves the running arena untouched until the next save.
    /// </summary>
    /// <remarks>
    /// Split out of <c>BeatEmUpScreen.PollHotReload</c> so the policy is a plain state machine with
    /// no <see cref="System.IO.FileSystemWatcher"/>, no content and no screen attached to it.
    /// </remarks>
    internal sealed class StageReloadPolicy
    {
        /// <summary>Frames a single change signal is retried for before giving up.</summary>
        public const int RetryBudget = 20;

        private int retries;

        /// <summary>Frames still left in the current budget (0 = idle).</summary>
        public int RemainingRetries => retries;

        /// <summary>
        /// Advances one frame. <paramref name="changeSignalled"/> re-arms the budget (a change
        /// arriving mid-retry restarts it); <paramref name="tryLoad"/> is attempted at most once per
        /// frame while budget remains. Returns true exactly on the frame the load succeeds, which is
        /// also when the budget is cleared.
        /// </summary>
        public bool ShouldRebuild(bool changeSignalled, Func<bool> tryLoad)
        {
            if (changeSignalled)
                retries = RetryBudget;

            if (retries <= 0)
                return false;

            retries--;
            if (!tryLoad())
                return false;

            retries = 0;
            return true;
        }
    }
}
