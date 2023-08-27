// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;

namespace Nethermind.State.Snap
{
    public class SlotsAndProofs: IDisposable
    {
        public PathWithStorageSlot[][] PathsAndSlots { get; set; }
        public byte[][] Proofs { get; set; }

        public IDisposable? Disposable { get; set; }
        public void Dispose()
        {
            Disposable?.Dispose();
        }
    }
}
