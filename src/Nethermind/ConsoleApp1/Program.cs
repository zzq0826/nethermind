// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Db.Rocks;
using Nethermind.Db.Rocks.Config;
using Nethermind.Logging;

var dirName = "F:\\db\\x\\blobTransactions";

if (Directory.Exists(dirName))
    Directory.Delete(dirName, true);

ColumnsDb<SomeEnum> columnsDb = new ColumnsDb<SomeEnum>("F:/db/x", StandardDbInitializer.BuildDbSettings(DbNames.BlobTransactions, () => { }, () => { }),
             new DbConfig(), LimboLogs.Instance, new List<SomeEnum>() { SomeEnum.Xxx });

var column = columnsDb.GetColumnDb(SomeEnum.Xxx) as ColumnDb ?? throw new Exception();
var bytes = new byte[128 * 1024 * 1024];
new Random().NextBytes(bytes);

const long N = 10;

for (long key = 0; key < N; key++)
{
    column.Set(KeyFrom(key), bytes);
}

Console.WriteLine("Added");
for (long key = 0; key < N; key++)
{

    column.Remove(KeyFrom(key));
}

Console.WriteLine("Deleted");

column.Flush();
Console.WriteLine("Flushed");
for (var i = 0; i < 30; i++)
{
    Console.WriteLine("Size: {0}", Directory.GetFiles(dirName, "*", SearchOption.AllDirectories).Sum(t => new FileInfo(t).Length));
    await Task.Delay(10_000);
}

static byte[] KeyFrom(long i)
{
    return i.ToBigEndianByteArrayWithoutLeadingZeros();
}

public enum SomeEnum
{
    Xxx
}
