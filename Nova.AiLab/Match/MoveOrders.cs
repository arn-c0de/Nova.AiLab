using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Simulation.CommandsV1;

namespace Nova.AiLab
{
    /// <summary>
    /// The one way a scenario walks units across the map: a move intent through
    /// the slot's OWN peer ingress, chunked to the command contract's limit.
    /// <para>
    /// The duel arena and the movement scenarios each carried their own copy of
    /// this loop. They agreed on the part that matters — orders travel the
    /// canonical sealed command path exactly like a human's, instead of poking
    /// entity state directly — and that is precisely why one copy is enough:
    /// the next fix to the submission path has one place to land, not two that
    /// can drift.
    /// </para>
    /// <para>
    /// THE VERDICT CANNOT COME FROM <c>TrySubmitIntent</c>. At a peer ingress
    /// that returns the SUBMISSION result, which is Accepted no matter what the
    /// host intake made of the record. Only the transport sees the intake
    /// verdict, so the refusal count is a delta on the counting transport
    /// around the submissions. An earlier version of this in the arena declared
    /// a local counter, never incremented it and returned zero — a refusal
    /// would have left both sides standing still and the row would have read as
    /// a stalemate finding instead of a broken setup.
    /// </para>
    /// </summary>
    internal static class MoveOrders
    {
        /// <summary>
        /// Sends <paramref name="raws"/> to a cell and returns how many records
        /// the HOST intake refused. Zero when the slot has no command seat or
        /// the run bound no counting transport — in the second case the number
        /// is unknown, not proven to be zero, which is why the arena turns
        /// counting on explicitly.
        /// </summary>
        public static int Submit(MultiSlotAiHost host, byte slot, List<uint> raws, int targetX, int targetY)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (raws == null || raws.Count == 0) return 0;

            SlotPeer peer = host.PeerOf(slot);
            if (peer == null) return 0;

            int before = peer.IntentCounter?.Rejected ?? 0;

            // Chunked to the command contract's per-payload entity limit; the
            // caller's sorted order keeps the split deterministic.
            const int chunk = CommandLimits.MaxEntityIdsPerCommand;
            for (int start = 0; start < raws.Count; start += chunk)
            {
                int length = Math.Min(chunk, raws.Count - start);
                var ids = new uint[length];
                raws.CopyTo(start, ids, 0, length);
                peer.Ingress.TrySubmitIntent(
                    CommandIntent.Create(new MovePayload(ids, SimFixed.FromInt(targetX), SimFixed.FromInt(targetY))),
                    out _);
            }

            return (peer.IntentCounter?.Rejected ?? 0) - before;
        }
    }
}
