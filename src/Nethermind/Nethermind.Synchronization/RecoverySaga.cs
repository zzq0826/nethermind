using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Consensus.Processing;
using Nethermind.Db;
using Nethermind.Synchronization.FastSync;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.StateSync;
using Nethermind.Trie;

namespace Nethermind.Synchronization
{
    public class RecoverySaga : IRecoverySaga
    {
        StateSyncDispatcher stateSyncDispatcher;
        private StateSyncFeed stateSyncFeed;
        MultiSyncModeSelector multiSyncModeSelector;
        TaskCompletionSource tcs;
        private IDb stateDb;

        public async Task RecoverFrom(SpecificTrieException ex)
        {
            switch (ex)
            {
                case MissingAccountNodeTrieException ac:
                    Console.WriteLine("~~~~~~ RECOVER");
                    tcs = new TaskCompletionSource();
                    stateSyncFeed.Recover(ex.BlockNumber, ex.Root, ac.AccountHash);
                    multiSyncModeSelector.IsRecovery = true;
                    await tcs.Task;
                    multiSyncModeSelector.IsRecovery = false;
                    stateSyncFeed.ResetRecovery();
                    break;

                case MissingStorageNodeTrieException st:
                    Console.WriteLine("~~~~~~ RECOVER");
                    tcs = new TaskCompletionSource();
                    stateSyncFeed.Recover(ex.BlockNumber, ex.Root, st.AccountHash, st.StorageHash);
                    multiSyncModeSelector.IsRecovery = true;
                    await tcs.Task;
                    multiSyncModeSelector.IsRecovery = false;
                    stateSyncFeed.ResetRecovery();
                    break;
                    //await stateSyncDispatcher.HandleSingleRequest(new StateSyncBatch(ex.Root, NodeDataType.State, new[] {
                    //    new StateSyncItem(mse.AccountHash, Nibbles.NibbleBytesFromBytes(mse.AccountHash.Bytes), Nibbles.NibbleBytesFromBytes(mse.StorageHash.Bytes), NodeDataType.Storage, 16) }), System.Threading.CancellationToken.None);
                    //break;
            }
        }

        public void RegisterStateFeed(StateSyncFeed stateSyncFeed)
        {
            this.stateSyncFeed = stateSyncFeed;
        }
        public void RegisterStateSyncDispatcher(StateSyncDispatcher stateSyncDispatcher)
        {
            this.stateSyncDispatcher = stateSyncDispatcher;
        }

        public void Register(MultiSyncModeSelector multiSyncModeSelector)
        {
            this.multiSyncModeSelector = multiSyncModeSelector;
        }

        internal void Register(IDb stateDb)
        {
            this.stateDb = stateDb;
        }

        private RecoverySaga()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    while(TreeSync.Ok.Any())
                    {
                        var el = TreeSync.Ok.First();
                        TreeSync.Ok.Remove(el);
                        TreeSync.Deleted.Add(el);
                        stateDb.Remove(el.Bytes);
                        Console.WriteLine("~~~~~~ DELET {0}", el);
                    }
                }
            });
        }

        public static RecoverySaga Instance { get; } = new RecoverySaga();

        internal void Finish()
        {
            tcs.SetResult();
        }
    }
}
