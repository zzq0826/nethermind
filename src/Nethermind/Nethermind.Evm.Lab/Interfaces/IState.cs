// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Evm.Lab.Interfaces;
public interface IState<T> where T : IState<T>, new()
{
    IState<T> Initialize(T seed) => new T();
    EventsSink EventsSink { get; }
    T GetState() => (T)this;
}
