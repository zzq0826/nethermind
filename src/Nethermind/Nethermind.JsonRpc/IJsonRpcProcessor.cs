// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Microsoft.AspNetCore.Http;

using System;
using System.Collections.Generic;
using System.IO.Pipelines;

namespace Nethermind.JsonRpc;

public interface IJsonRpcProcessor
{
    IAsyncEnumerable<JsonRpcResult> ProcessAsync(HttpRequest? http, PipeReader stream, JsonRpcContext context);
}
