//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Synchronization.Test;
using NUnit.Framework;

namespace Nethermind.Merge.Plugin.Test.Synchronization;

[TestFixture(SynchronizerType.Fast)]
[TestFixture(SynchronizerType.Full)]
[Parallelizable(ParallelScope.All)]
public class MergeSynchronizerTests
{
    private readonly SynchronizerType _synchronizerType;
    
    public MergeSynchronizerTests(SynchronizerType synchronizerType)
    {
        _synchronizerType = synchronizerType;
    }
    
    private static Block _genesisBlock = Build.A.Block.Genesis.WithDifficulty(100000)
        .WithTotalDifficulty((UInt256)100000).TestObject;
    
    private WhenImplementation When => new(_synchronizerType);

    private class WhenImplementation
    {
        private readonly SynchronizerType _synchronizerType;

        public WhenImplementation(SynchronizerType synchronizerType)
        {
            _synchronizerType = synchronizerType;
        }

        public MergeSyncingContext Syncing => new(_synchronizerType);
    }
}
