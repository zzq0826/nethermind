using System;
using System.Collections.Generic;
using System.Text;
using Nethermind.Config;

namespace Nethermind.Db.Rocks.Config
{
    [ConfigCategory(HiddenFromDocs = true)]
    public interface IPlugableDbConfig
    {
        ulong WriteBufferSize { get; set; }
        uint WriteBufferNumber { get; set; }
        ulong BlockCacheSize { get; set; }
        bool CacheIndexAndFilterBlocks { get; set; }

        uint RecycleLogFileNum { get; set; }
        bool WriteAheadLogSync { get; set; }
    }
}
