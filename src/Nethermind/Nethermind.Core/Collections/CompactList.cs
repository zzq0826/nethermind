// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;

namespace Nethermind.Core.Collections;

/// <summary>
/// An IList implementation that internally is an array of array.
/// "but why?" you might ask... well my friend, its to prevent Large Object Heap allocation, **nasty little bugger**.
/// This class lets you specify the size of the inner array so that its not big enough to become a LOH.
/// Also, pool the inner array by default.
/// </summary>
/// <typeparam name="T"></typeparam>
public class CompactList<T>: IList<T>
{
    private ArrayPool<T> _pool;
    private readonly int _itemPerInnerNode;
    private T[]?[] _arrayOfArray;
    private int _count;

    public CompactList(int itemPerInnerNode = 8 * 1028, ArrayPool<T>? pool = null)
    {
        _itemPerInnerNode = itemPerInnerNode;
        _arrayOfArray = Array.Empty<T[]?>();
        _pool = pool ?? ArrayPool<T>.Shared;
        _count = 0;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    private void ExpandForLength(int requiredLength)
    {
        int neededInnerLength = (_itemPerInnerNode + requiredLength) / _itemPerInnerNode;

        if (neededInnerLength <= _arrayOfArray.Length) return;

        T[]?[] newArrayOfArray = new T[Math.Max(_arrayOfArray.Length * 2, 1)][];
        _arrayOfArray.CopyTo(newArrayOfArray.AsSpan());
        _arrayOfArray = newArrayOfArray;
    }

    public void Add(T item)
    {
        ExpandForLength(_count+1);

        int slotIdx = _count / _itemPerInnerNode;
        _arrayOfArray[slotIdx] ??= _pool.Rent(_itemPerInnerNode);
        _arrayOfArray[slotIdx]![_count%_itemPerInnerNode] = item;
        _count++;
    }

    public void Clear()
    {
        foreach (T[]? innerArray in _arrayOfArray)
        {
            if (innerArray == null) break;
            _pool.Return(innerArray);
        }
        _arrayOfArray = Array.Empty<T[]>();
        _count = 0;
    }

    public bool Contains(T item)
    {
        return IndexOf(item) != -1;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array.Length - arrayIndex < _count) throw new ArgumentException("insufficient space in target");

        for (int i = 0; i < _arrayOfArray.Length; i++)
        {
            if (_arrayOfArray[i] == null) break;

            if (_count / _itemPerInnerNode == i)
            {
                // last one
                _arrayOfArray[i]!.AsSpan(0, _count % _itemPerInnerNode).CopyTo(array.AsSpan());
            }
            else
            {
                _arrayOfArray[i]!.CopyTo(array, arrayIndex + i * _itemPerInnerNode);
            }
        }

    }

    public bool Remove(T item)
    {
        int idx = IndexOf(item);
        if (idx == -1) return false;

        RemoveAt(idx);
        return true;
    }

    public int Count => _count;
    public bool IsReadOnly => false;
    public int IndexOf(T item)
    {
        for (int i = 0; i < _arrayOfArray.Length; i++)
        {
            if (_arrayOfArray[i] == null) break;
            int innerIndex = Array.IndexOf(_arrayOfArray[i]!, item);
            if (innerIndex != -1) return i * _itemPerInnerNode + innerIndex;
        }

        return -1;
    }

    public void Insert(int index, T item)
    {
        if (index < 0 || index > _count) throw new IndexOutOfRangeException();
        ShiftRight(index);
        this[index] = item;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index > _count) throw new IndexOutOfRangeException();
        ShiftLeft(index);
    }

    private void ShiftRight(int startingPosition)
    {
        _count++;
        ExpandForLength(_count);
        _arrayOfArray[_count / _itemPerInnerNode] ??= _pool.Rent(_itemPerInnerNode);

        for (int i = _count-1; i > startingPosition; i--)
        {
            this[i] = this[i-1];
        }
    }

    private void ShiftLeft(int startingPosition)
    {
        for (int i = startingPosition; i < _count-1; i++)
        {
            this[i] = this[i + 1];
        }

        // Maybe shrink
        if (_count % _itemPerInnerNode == 0)
        {
            int prevLastArray = _count / _itemPerInnerNode;
            T[] lastArr = _arrayOfArray[prevLastArray]!;
            _pool.Return(lastArr);
            _arrayOfArray[prevLastArray] = null;
        }
        _count--;

        // Not shrinking _arrayOfArray. Its probably fine.
    }

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
            return _arrayOfArray[index / _itemPerInnerNode]![index % _itemPerInnerNode];
        }
        set
        {
            if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
            _arrayOfArray[index / _itemPerInnerNode]![index % _itemPerInnerNode] = value;
        }
    }

    public void Sort(Comparison<T> comparer)
    {
        Sort(Comparer<T>.Create(comparer));
    }

    public void Sort(IComparer<T>? comparer = null)
    {
        ListSortHelper<T>.Default.Sort(this, 0, _count, comparer);
    }
}
