// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethermind.Consensus.Transactions;
using Nethermind.Consensus;

namespace Nethermind.Api.Extensions;

public interface IShutterPlugin : INethermindPlugin
{
    Task<IBlockProducer> InitBlockProducer(IConsensusWrapperPlugin consensusPlugin, IConsensusPlugin p);
}
