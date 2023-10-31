// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Synchronization.FastBlocks;

internal enum FastBlockStatus : byte
{
    BodiesPending = 0,
    BodiesRequestSent = 1,
    BodiesInserted = 2,
    ReceiptRequestSent = 3,
    ReceiptInserted = 4,
}
