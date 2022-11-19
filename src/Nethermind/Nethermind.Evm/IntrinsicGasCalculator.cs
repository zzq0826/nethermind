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

using System;
using System.IO;
using System.Linq;
using Nethermind.Core;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Specs;
using Nethermind.Int256;

namespace Nethermind.Evm
{
    public static class IntrinsicGasCalculator
    {
        public static long Calculate(Transaction transaction, IReleaseSpec releaseSpec/*, UInt256 parentExcessDataGas*/)
        {
            long result = GasCostOf.Transaction;
            result += DataCost(transaction, releaseSpec);
            result += CreateCost(transaction, releaseSpec);
            result += AccessListCost(transaction, releaseSpec);
            return result;
        }

        private static long CreateCost(Transaction transaction, IReleaseSpec releaseSpec)
        {
            long createCost = 0;
            if (transaction.IsContractCreation && releaseSpec.IsEip2Enabled)
            {
                createCost += GasCostOf.TxCreate;
            }

            return createCost;
        }

        private static long DataCost(Transaction transaction, IReleaseSpec releaseSpec)
        {
            long txDataNonZeroGasCost =
                releaseSpec.IsEip2028Enabled ? GasCostOf.TxDataNonZeroEip2028 : GasCostOf.TxDataNonZero;
            long dataCost = 0;
            if (transaction.Data != null)
            {
                for (int i = 0; i < transaction.Data.Length; i++)
                {
                    dataCost += transaction.Data[i] == 0 ? GasCostOf.TxDataZero : txDataNonZeroGasCost;
                }
            }

            return dataCost;
        }

        private static long AccessListCost(Transaction transaction, IReleaseSpec releaseSpec)
        {
            AccessList? accessList = transaction.AccessList;
            long accessListCost = 0;
            if (accessList is not null)
            {
                if (releaseSpec.UseTxAccessLists)
                {
                    if (accessList.IsNormalized)
                    {
                        accessListCost += accessList.Data.Count * GasCostOf.AccessAccountListEntry;
                        accessListCost += accessList.Data.Sum(d => d.Value.Count) *
                                          GasCostOf.AccessStorageListEntry;
                    }
                    else
                    {
                        foreach (object o in accessList.OrderQueue!)
                        {
                            if (o is Address)
                            {
                                accessListCost += GasCostOf.AccessAccountListEntry;
                            }
                            else
                            {
                                accessListCost += GasCostOf.AccessStorageListEntry;
                            }
                        }
                    }
                }
                else
                {
                    throw new InvalidDataException(
                        $"Transaction with an access list received within the context of {releaseSpec.Name}. Eip-2930 is not enabled.");
                }
            }

            return accessListCost;
        }

        static UInt256 DataGasPriceUpdateFraction = 8902606;
        static UInt256 MinDataGasPrice = (long)10e8;

        // FakeExponential
        public static UInt256 BlobsGas(Transaction transaction, UInt256 parentExcessDataGas, IReleaseSpec releaseSpec)
        {
            if (!releaseSpec.IsEip4844Enabled || transaction.Type != TxType.Blob || transaction.BlobVersionedHashes?.Any() != true)
            {
                return 0;
            }
            if(parentExcessDataGas == UInt256.MaxValue)
            {
                throw new InvalidOperationException();
            }

            return (ulong)transaction.BlobVersionedHashes!.Length * CostPerBlob(parentExcessDataGas);
        }

        public static UInt256 CostPerBlob(UInt256 parentExcessDataGas)
        {
            UInt256 FakeExponential(UInt256 factor, UInt256 num, UInt256 denom)
            {
                UInt256 output = UInt256.Zero;

                UInt256 numAccum = factor * denom;

                for (UInt256 i = 1; numAccum > 0; i++)
                {
                    output += numAccum;
                    numAccum *= num;
                    numAccum /= i * denom;
                }
                return output / denom;
            }

            return FakeExponential(MinDataGasPrice, parentExcessDataGas, DataGasPriceUpdateFraction);
        }

        static UInt256 DataGasPerBlob = 1 << 17;
        static UInt256 TargetDataGasPerBlock = 1 << 20;

        public static UInt256 CalcExcessDataGas(UInt256? parentExcessDataGas, int newBlobs)
        {
            UInt256 excessDataGas = parentExcessDataGas.GetValueOrDefault();
            UInt256 consumedGas = DataGasPerBlob * (UInt256)newBlobs;
            excessDataGas += consumedGas;
            return excessDataGas < TargetDataGasPerBlock ? UInt256.Zero : (excessDataGas - TargetDataGasPerBlock);
        }
    }
}
