using System;
using System.Threading;
using NUnit.Framework;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Re-entrancy gauge for the SafeDictionary events-under-lock fix (Operation Earthfall P0.5, findings/A1-freeze.md
    /// H2). Before the fix, <see cref="SafeDictionary{TKey,TValue}"/> raised its ItemAdded/ItemRemoved/OnChange events
    /// while STILL holding its internal Monitor lock, so a subscriber ran arbitrary code under the lock — a
    /// cross-thread deadlock/contention hazard on the parallel sim threads. The fix captures the handler under the
    /// lock but INVOKES it after releasing the lock (copy-then-notify).
    ///
    /// The distinguishing move: from INSIDE the notification, spin up a SEPARATE thread that reads the same dictionary
    /// (which takes the lock) and wait for it to finish. Monitor is reentrant for the SAME thread, so the reader MUST
    /// be a different thread to actually block on a held lock. Under the OLD (under-lock) behaviour the reader can
    /// never acquire the lock while the notifying thread holds it, so the bounded Join returns false and the test
    /// FAILS. Under the fixed behaviour the lock is already released, the reader completes, and the test PASSES.
    /// The inner Join is bounded (never an unbounded hang), and <c>[Timeout]</c> is a backstop.
    /// </summary>
    [TestFixture]
    public class SafeDictionaryEventLockTests
    {
        // How long a concurrent reader is given to finish from inside a notification. Comfortably longer than a lock
        // acquire on a released lock, short enough that an under-lock regression fails fast.
        private const int ReaderJoinTimeoutMs = 3000; // FLAGGED balance value (test tuning, not gameplay)

        /// <summary>
        /// Launches a background thread that reads <paramref name="dict"/> (which takes the internal lock) and blocks
        /// until it terminates or the timeout elapses. Returns true if the reader completed — i.e. it was able to
        /// acquire the lock, which it can only do if the notifying thread is NOT holding it.
        /// </summary>
        private static bool ConcurrentReaderCompletes(SafeDictionary<int, string> dict, int probeKey)
        {
            Exception? readerError = null;
            var reader = new Thread(() =>
            {
                try
                {
                    // Both of these take lock(_lock) inside SafeDictionary.
                    var _ = dict.Count;
                    var __ = dict.ContainsKey(probeKey);
                }
                catch (Exception ex)
                {
                    readerError = ex;
                }
            });
            reader.IsBackground = true;
            reader.Start();
            bool completed = reader.Join(ReaderJoinTimeoutMs);
            Assert.IsNull(readerError, "The concurrent reader threw: " + readerError);
            return completed;
        }

        [Test, Timeout(30000)]
        public void Add_RaisesItemAdded_WithoutHoldingLock()
        {
            var dict = new SafeDictionary<int, string>();
            bool readerCompleted = false;
            bool handlerRan = false;

            dict.ItemAdded += (key, value) =>
            {
                handlerRan = true;
                readerCompleted = ConcurrentReaderCompletes(dict, key);
            };

            dict.Add(1, "a");

            Assert.IsTrue(handlerRan, "ItemAdded did not fire.");
            Assert.IsTrue(readerCompleted,
                "A concurrent reader could not acquire the lock during ItemAdded — the event fired while the lock was held.");
        }

        [Test, Timeout(30000)]
        public void Remove_RaisesItemRemoved_WithoutHoldingLock()
        {
            var dict = new SafeDictionary<int, string>();
            dict.Add(1, "a");

            bool readerCompleted = false;
            bool handlerRan = false;

            dict.ItemRemoved += (key, value) =>
            {
                handlerRan = true;
                readerCompleted = ConcurrentReaderCompletes(dict, key);
            };

            bool removed = dict.Remove(1);

            Assert.IsTrue(removed, "Remove reported the key was absent.");
            Assert.IsTrue(handlerRan, "ItemRemoved did not fire.");
            Assert.IsTrue(readerCompleted,
                "A concurrent reader could not acquire the lock during ItemRemoved — the event fired while the lock was held.");
        }

        [Test, Timeout(30000)]
        public void IndexerSet_RaisesOnChange_WithoutHoldingLock()
        {
            var dict = new SafeDictionary<int, string>();
            bool readerCompleted = false;
            bool handlerRan = false;

            dict.OnChange += (key, value) =>
            {
                handlerRan = true;
                readerCompleted = ConcurrentReaderCompletes(dict, key);
            };

            dict[7] = "seven";

            Assert.IsTrue(handlerRan, "OnChange did not fire on indexer set.");
            Assert.IsTrue(readerCompleted,
                "A concurrent reader could not acquire the lock during OnChange — the event fired while the lock was held.");
        }

        [Test, Timeout(30000)]
        public void Add_StillNotifiesSubscribersWithCorrectArgs()
        {
            // Correctness backstop: copy-then-notify must still deliver the notification with the right key/value,
            // and the mutation must be visible to a subscriber that reads the dictionary (notify happens post-mutate).
            var dict = new SafeDictionary<int, string>();
            int? seenKey = null;
            string? seenValue = null;
            bool presentAtNotify = false;

            dict.ItemAdded += (key, value) =>
            {
                seenKey = key;
                seenValue = value;
                presentAtNotify = dict.ContainsKey(key); // mutation already applied under the lock
            };

            dict.Add(42, "answer");

            Assert.AreEqual(42, seenKey);
            Assert.AreEqual("answer", seenValue);
            Assert.IsTrue(presentAtNotify, "Item was not yet present when ItemAdded fired.");
        }
    }
}
