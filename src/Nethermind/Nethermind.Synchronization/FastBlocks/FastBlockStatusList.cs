// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Nethermind.Synchronization.Test")]
namespace Nethermind.Synchronization.FastBlocks;

internal class FastBlockStatusList
{
    private readonly long[] _statuses;
    private readonly long _length;

    public FastBlockStatusList(long length)
    {
        // Can fit 21 statuses per long, however need to round up the division
        long size = length / 21;
        if (size * 21 < length)
        {
            size++;
        }

        _statuses = new long[size];
        _length = length;
    }

    internal FastBlockStatusList(IList<FastBlockStatus> statuses, bool parallel) : this(statuses.Count)
    {
        if (parallel)
        {
            Parallel.For(0, statuses.Count, i =>
            {
                this[i] = statuses[i];
            });
        }
        else
        {
            for (int i = 0; i < statuses.Count; i++)
            {
                this[i] = statuses[i];
            }
        }
    }

    public FastBlockStatus this[long index]
    {
        get
        {
            GuardLength(index);
            (long position, int shift) = GetValuePosition(index);
            long status = Volatile.Read(ref _statuses[position]);
            FastBlockStatus decoded = DecodeValue(status, shift);;
            return decoded;
        }
        internal set
        {
            GuardLength(index);
            (long position, int shift) = GetValuePosition(index);
            ref long status = ref _statuses[position];
            long oldValue = Volatile.Read(ref status);
            do
            {
                long newValue = EncodeValue(value, oldValue, shift);
                long currentValue = Interlocked.CompareExchange(ref status, newValue, oldValue);
                if (currentValue == oldValue || DecodeValue(currentValue, shift) == value)
                {
                    // Change has happened
                    break;
                }
                // Change not done, set old value to current value
                oldValue = currentValue;
            } while (true);
        }
    }

    private void GuardLength(long index)
    {
        if ((ulong)index >= (ulong)_length)
        {
            ThrowIndexOutOfRange();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (long position, int shift) GetValuePosition(long index)
    {
        (long position, long shift) = Math.DivRem(index, 21);
        return (position, (int)shift * 3); // 3 bits per status
    }

    public bool TrySet(long index, FastBlockStatus newState) => TrySet(index, newState, out _);

    private bool CanTransition(FastBlockStatus priorState, FastBlockStatus newState)
    {
        return priorState switch
        {
            FastBlockStatus.BodiesPending => newState is FastBlockStatus.BodiesRequestSent,
            FastBlockStatus.BodiesRequestSent => newState is FastBlockStatus.BodiesPending or FastBlockStatus.BodiesInserted,
            FastBlockStatus.BodiesInserted => newState is FastBlockStatus.ReceiptRequestSent,
            FastBlockStatus.ReceiptRequestSent => newState is FastBlockStatus.ReceiptInserted or FastBlockStatus.BodiesInserted,
            FastBlockStatus.ReceiptInserted => false,
            _ => throw new ArgumentOutOfRangeException(nameof(newState), newState, null)
        };
    }

    public bool TrySet(long index, FastBlockStatus newState, out FastBlockStatus previousValue)
    {
        GuardLength(index);

        (long position, int shift) = GetValuePosition(index);
        ref long status = ref _statuses[position];
        long oldValue = Volatile.Read(ref status);
        do
        {
            previousValue = DecodeValue(oldValue, shift);
            if (!CanTransition(previousValue, newState))
            {
                // Change not possible
                return false;
            }

            long newValue = EncodeValue(newState, oldValue, shift);
            long previousValueInt = Interlocked.CompareExchange(ref status, newValue, oldValue);
            if (previousValueInt == oldValue)
            {
                // Change has happened
                return true;
            }

            previousValue = DecodeValue(previousValueInt, shift);
            if (previousValue == newState)
            {
                // Change has happened but not us
                return false;
            }

            // Change not done, set old value to current value
            oldValue = previousValueInt;
        } while (true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FastBlockStatus DecodeValue(long value, int shift) => (FastBlockStatus)((value >> shift) & 0b111);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long EncodeValue(FastBlockStatus newState, long oldValue, int shift) => (oldValue & ~((long)0b111 << shift)) | (((long)newState) << shift);

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowIndexOutOfRange()
    {
        throw new IndexOutOfRangeException();
    }
}
