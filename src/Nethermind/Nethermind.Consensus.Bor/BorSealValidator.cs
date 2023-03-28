// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Consensus.Bor;

public class BorSealValidator : ISealValidator
{
    public bool ValidateParams(BlockHeader parent, BlockHeader header, bool isUncle = false)
    {
        return true;
    }

    public bool ValidateSeal(BlockHeader header, bool force)
    {
        return true;
    }
}