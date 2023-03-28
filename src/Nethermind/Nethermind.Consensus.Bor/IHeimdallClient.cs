// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Consensus.Bor;

public interface IHeimdallClient
{
    // TODO: Make this async
    HeimdallSpan GetSpan(long number);
    StateSyncEventRecord[] StateSyncEvents(ulong fromId, ulong toTime);
}