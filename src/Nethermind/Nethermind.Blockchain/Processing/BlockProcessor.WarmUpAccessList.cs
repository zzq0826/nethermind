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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Eip2930;
using Nethermind.Int256;

namespace Nethermind.Blockchain.Processing
{
    public partial class BlockProcessor
    {
        private void WarmUpAccessList(Block block, CancellationToken cancellationToken)
        {
            Task.Run(() =>
            {
                foreach (Transaction transaction in block.Transactions)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        AccessList? accessList = transaction.AccessList;
                        if (accessList is not null)
                        {
                            foreach (KeyValuePair<Address, IReadOnlySet<UInt256>> accessedAddress in accessList.Data)
                            {
                                _stateProvider.WarmUpAccount(accessedAddress.Key);
                                
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    foreach (UInt256 storageIndex in accessedAddress.Value)
                                    {
                                        _storageProvider.WarmUpCell(new StorageCell(accessedAddress.Key, storageIndex));
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }, cancellationToken);
        }
    }
}
