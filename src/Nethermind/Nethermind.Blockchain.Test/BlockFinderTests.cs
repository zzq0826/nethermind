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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using FluentAssertions;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test;

[Parallelizable(ParallelScope.All)]
public class BlockFinderTests
{
    private static IBlockTree Tree => Build.A.BlockTree().OfChainLength(3).TestObject;

    public static IEnumerable<TestCaseData> HeaderTests
    {
        get
        {
            TestCaseData GetHeadCase(Expression<Func<IBlockFinder, BlockHeader>> getHeader)
            {
                return new TestCaseData(getHeader.Compile(), (IBlockTree t) => t.Head!.Header) { TestName = getHeader.GetName() };
            }
            
            TestCaseData GetGenesisCase(Expression<Func<IBlockFinder, BlockHeader>> getHeader)
            {
                return new TestCaseData(getHeader.Compile(), (IBlockTree t) => t.Genesis) { TestName = getHeader.GetName() };
            }
            
            yield return GetHeadCase(f => f.FindLatestHeader());  
            yield return GetHeadCase(f => f.FindHeader(new BlockParameter(3), false));
            yield return GetHeadCase(f => f.FindHeader(new BlockParameter(Tree.HeadHash!, false), false));
            yield return GetHeadCase(f => f.FindHeader(3));
            yield return GetHeadCase(f => f.FindHeader(3, BlockTreeLookupOptions.None));
            yield return GetHeadCase(f => f.FindHeader(Tree.HeadHash!));
            yield return GetHeadCase(f => f.FindHeader(Tree.HeadHash!, BlockTreeLookupOptions.None));
            yield return GetHeadCase(f => f.FindPendingHeader());
            yield return GetHeadCase(f => f.FindBestSuggestedHeader());
            yield return GetGenesisCase(f => f.FindEarliestHeader());
            yield return GetGenesisCase(f => f.FindGenesisHeader());
        }
    }



    [TestCaseSource(nameof(HeaderTests))]
    public void Returns_copy(Func<IBlockFinder, BlockHeader> getHeader, Func<IBlockTree, BlockHeader> getExpected)
    {
        IBlockTree tree = Tree;
        BlockHeader header = getHeader(tree);
        BlockHeader head = getExpected(tree);
        header.Should().NotBeSameAs(head);
        header.Should().BeEquivalentTo(head);
    }
}
