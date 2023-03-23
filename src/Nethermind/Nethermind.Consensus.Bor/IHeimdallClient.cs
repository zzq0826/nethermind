// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Consensus.Bor;

public interface IHeimdallClient
{
    HeimdallSpan? GetSpan(long number);
}