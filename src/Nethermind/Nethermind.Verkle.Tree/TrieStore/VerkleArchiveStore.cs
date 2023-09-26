// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Verkle.Tree.TrieStore;

public class VerkleArchiveStore
{
    private VerkleStateStore _stateStore;
    public VerkleArchiveStore(VerkleStateStore stateStore)
    {
        _stateStore = stateStore;
    }



}
