// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Consensus.Bor;

public interface IBorParamsHelper
{
    bool IsSprintStart(long blockNumber);
    
    long CalculateSprintSize(long blockNumber);
}