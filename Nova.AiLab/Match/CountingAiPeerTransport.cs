using System;
using Nova.Simulation.CommandsV1;

namespace Nova.AiLab
{
    /// <summary>
    /// A counting stand-in for <c>AiPeerCommandTransport</c>, used only when a
    /// run collects metrics.
    /// <para>
    /// WHY THIS EXISTS. <c>intentsRejected</c> is the underestimated number of
    /// plan section 3.3: it shows where the AI runs into executor rules,
    /// silently, because <c>TrySubmitIntent</c> deliberately does not evaluate
    /// the host verdict. The obvious cheap derivation — submitted sequences
    /// minus the host's sealed watermark — is WRONG, and quietly so: the
    /// watermark is a high-water mark, not a count. A rejected sequence in the
    /// middle of the stream leaves a gap that later accepted records seal
    /// straight past, and the rejection disappears from the arithmetic. The
    /// only honest place to count a verdict is where the verdict happens.
    /// </para>
    /// <para>
    /// SCOPE. This uses the <see cref="ICommandTransport"/> contract, it does
    /// not change it, and it does not touch <c>AiPeerCommandTransport</c> —
    /// that file is repository code and would never reach a PR from a lab
    /// branch. The forwarding body is the same three lines: deliver the peer's
    /// record bytes into the host ingress's validating intake.
    /// </para>
    /// <para>
    /// PROOF THAT IT COSTS NOTHING. Counting must not change the match. A run
    /// with and without this transport must produce the identical hash chain —
    /// asserted in the test suite, the same condition the plan puts on the view
    /// recorder (section 3.4): "as a test, not as an intention".
    /// </para>
    /// </summary>
    public sealed class CountingAiPeerTransport : ICommandTransport
    {
        private readonly CommandIngress _hostIngress;

        /// <summary>Records handed to the host intake.</summary>
        public int Submitted { get; private set; }

        /// <summary>Records the host intake accepted.</summary>
        public int Accepted { get; private set; }

        /// <summary>Records the host intake refused — the number section 3.3 is after.</summary>
        public int Rejected { get; private set; }

        /// <summary>Reason of the most recent rejection, for the findings list.</summary>
        public CommandRejectReason LastRejectReason { get; private set; }

        public CountingAiPeerTransport(CommandIngress peerIngress, CommandIngress hostIngress)
        {
            if (peerIngress == null) throw new ArgumentNullException(nameof(peerIngress));
            _hostIngress = hostIngress ?? throw new ArgumentNullException(nameof(hostIngress));
            peerIngress.BindTransport(this);
        }

        public void Send(byte[] recordBytes)
        {
            Submitted++;
            CommandIngressResult result = _hostIngress.TryAcceptRecordBytes(recordBytes, out CommandRejectReason reason);
            if (result == CommandIngressResult.Rejected)
            {
                Rejected++;
                LastRejectReason = reason;
            }
            else
            {
                Accepted++;
            }
        }
    }
}
