using System;
using Contraption.Domain.Blueprints;

namespace Contraption.Domain.Editing
{
    /// <summary>
    /// The outcome of one editor action: either a new blueprint, or a refusal with a reason the
    /// player can read.
    ///
    /// There is no third case. An edit never partially applies, and never fails silently —
    /// silent rejection was a recorded failure of the previous project (`docs/ISSUES.md`), and
    /// this type exists so the UI cannot accidentally reproduce it: getting at the blueprint
    /// requires acknowledging that it might not be there.
    /// </summary>
    public sealed class EditResult
    {
        private readonly ContraptionBlueprint? _blueprint;

        private EditResult(ContraptionBlueprint? blueprint, string? rejectionReason)
        {
            _blueprint = blueprint;
            RejectionReason = rejectionReason;
        }

        public bool Accepted => _blueprint is not null;

        /// <summary>Player-readable, and non-null exactly when the edit was rejected.</summary>
        public string? RejectionReason { get; }

        /// <summary>The resulting blueprint. Throws if the edit was rejected.</summary>
        public ContraptionBlueprint Blueprint =>
            _blueprint ?? throw new InvalidOperationException(
                $"This edit was rejected ({RejectionReason}); there is no resulting blueprint.");

        public static EditResult Accept(ContraptionBlueprint blueprint)
        {
            if (blueprint is null)
            {
                throw new ArgumentNullException(nameof(blueprint));
            }

            return new EditResult(blueprint, null);
        }

        public static EditResult Reject(string playerReadableReason)
        {
            if (string.IsNullOrWhiteSpace(playerReadableReason))
            {
                throw new ArgumentException(
                    "A rejection must explain itself to the player.", nameof(playerReadableReason));
            }

            return new EditResult(null, playerReadableReason);
        }
    }
}
