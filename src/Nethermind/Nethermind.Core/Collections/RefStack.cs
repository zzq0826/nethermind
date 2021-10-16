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
// 

using System;
using System.Buffers;
using System.Collections.Generic;

namespace Nethermind.Core.Collections
{
    public ref struct RefStack<T>
    {
        private T[] _array;
        
        public int Count { get; private set; }
        
        public RefStack(int initialCapacity)
        {
            _array = ArrayPool<T>.Shared.Rent(initialCapacity);
            Count = 0;
        }

        public void Push(T item)
        {
            if (Count == _array.Length)
            {
                T[] newArray = ArrayPool<T>.Shared.Rent(_array.Length * 2);
                _array.CopyTo(newArray, 0);
                _array = newArray;
                ArrayPool<T>.Shared.Return(_array);
            }
            
            _array[Count++] = item;
        }
        
        public T Pop()
        {
            if (Count == 0)
            {
                throw new IndexOutOfRangeException();
            }

            return _array[--Count];
        }
        
        public bool TryPop(out T? item)
        {
            if (Count == 0)
            {
                item = default;
                return false;
            }
            else
            {
                item = _array[--Count];
                return true;
            }
        }
        
        public T? Peek() => Count == 0 ? default : _array[Count - 1];

        public Span<T>.Enumerator GetEnumerator() => _array.AsSpan().Slice(0, Count).GetEnumerator();

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(_array);
        }
    }
}
