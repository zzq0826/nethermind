using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethermind.Core.Crypto;
using Nethermind.Trie.Pruning;

namespace Nethermind.Synchronization.Healing
{
    public interface IHealingFeed
    {
        public static IHealingFeed Instance;
        void RecoverAccount(Keccak accountHash, Keccak rootHash);
        void RecoverStorageSlot(Keccak storageHash, Keccak accountHash, Keccak rootHash);
    }

    //public class HealingFeedStub: IHealingFeed
    //{
    //    public bool UseDeplay = true;

    //    public HealingFeedStub()
    //    {
    //        if(IHealingFeed.Instance is not null)
    //        {
    //            throw new InvalidOperationException("HealingFeedStub is misused");
    //        }
    //        IHealingFeed.Instance = this;
    //    }

    //    public void RecoverAccount(Keccak accountHash, Keccak rootHash) {
    //        if (!UseDeplay) {
    //            TrieStore.Tried.Remove(rootHash);
    //            TrieStore.OK.Add(rootHash);
    //            TrieStore.Tried.Remove(accountHash);
    //            TrieStore.OK.Add(accountHash);

    //            return;
    //        }
    //        _ = Task.Delay(30).ContinueWith((t) =>
    //        {
    //            TrieStore.Tried.Remove(rootHash);
    //            TrieStore.OK.Add(rootHash);
    //            TrieStore.Tried.Remove(accountHash);
    //            TrieStore.OK.Add(accountHash);

    //        });
    //    }

    //    public void RecoverStorageSlot(Keccak storageHash, Keccak accountHash, Keccak rootHash) {
    //        if (!UseDeplay)
    //        {
    //            TrieStore.Tried.Remove(rootHash);
    //            TrieStore.OK.Add(rootHash);
    //            TrieStore.Tried.Remove(accountHash);
    //            TrieStore.OK.Add(accountHash);
    //            TrieStore.Tried.Remove(storageHash);
    //            TrieStore.OK.Add(storageHash);
    //            return;
    //        }
    //        _ = Task.Delay(30).ContinueWith((t) =>
    //        {
    //            TrieStore.Tried.Remove(rootHash);
    //            TrieStore.OK.Add(rootHash);
    //            TrieStore.Tried.Remove(accountHash);
    //            TrieStore.OK.Add(accountHash);
    //            TrieStore.Tried.Remove(storageHash);
    //            TrieStore.OK.Add(storageHash);
    //        });
    //    }
    //}
}
