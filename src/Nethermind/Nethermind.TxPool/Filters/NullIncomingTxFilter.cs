// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.TxPool.Filters
{
    public class NullIncomingTxFilter : IIncomingTxFilterAgnosticToState
    {
        private NullIncomingTxFilter() { }

        public static IIncomingTxFilterAgnosticToState Instance { get; } = new NullIncomingTxFilter();

        public AcceptTxResult Accept(Transaction tx, TxHandlingOptions txHandlingOptions)
        {
            return AcceptTxResult.Accepted;
        }
    }
}
