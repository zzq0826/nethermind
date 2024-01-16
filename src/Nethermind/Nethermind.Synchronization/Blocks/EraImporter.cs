// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO.Abstractions;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.State.Proofs;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Nethermind.Era1;
using Nethermind.Synchronization.FastBlocks;
using Nethermind.Blockchain.Synchronization;

namespace Nethermind.Synchronization;
public class EraImporter : IEraImporter
{
    private const int MergeBlock = 15537393;
    private readonly IFileSystem _fileSystem;
    private readonly IBlockTree _blockTree;
    private readonly IBlockValidator _blockValidator;
    private readonly IReceiptStorage _receiptStorage;
    private readonly ISpecProvider _specProvider;
    private readonly int _epochSize;
    private readonly string _networkName;
    private readonly long _bodyBarrier;
    private readonly long _receiptBarrier;
    private readonly bool _insertBodies;
    private readonly bool _insertReceipts;

    public event EventHandler<ImportProgressChangedArgs> ImportProgressChanged;

    public EraImporter(
        IFileSystem fileSystem,
        IBlockTree blockTree,
        IBlockValidator blockValidator,
        IReceiptStorage receiptStorage,
        ISpecProvider specProvider,
        ISyncConfig syncConfig,
        string networkName,
        int epochSize = EraWriter.MaxEra1Size)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _blockTree = blockTree;
        _blockValidator = blockValidator;
        _receiptStorage = receiptStorage ?? throw new ArgumentNullException(nameof(receiptStorage));
        _specProvider = specProvider ?? throw new ArgumentNullException(nameof(specProvider));
        this._epochSize = epochSize;
        if (string.IsNullOrWhiteSpace(networkName)) throw new ArgumentException("Cannot be null or whitespace.", nameof(specProvider));
        _networkName = networkName.Trim().ToLower();
        _bodyBarrier = syncConfig.AncientBodiesBarrierCalc;
        _receiptBarrier = syncConfig.AncientReceiptsBarrierCalc;
        _insertBodies = syncConfig.DownloadBodiesInFastSync;
        _insertReceipts = syncConfig.DownloadReceiptsInFastSync;
    }

    public Task ImportAsArchiveSync(string src, CancellationToken cancellation)
    {
        Hash256 header = _blockTree.Head.Hash;
        return ImportFull(
            src: src,
            startNumber: _blockTree.Head?.Number + 1 ?? 0,
            insertBodies: true,
            insertReceipts: false,
            expectedHeader: header,
            cancellation: cancellation);
    }

    public Task Import(string src, CancellationToken cancellation)
    {
        return ImportBackfill(
            src: src,
            bodyBarrier: _bodyBarrier,
            receiptsBarrier: _receiptBarrier,
            insertBodies: _insertBodies,
            insertReceipts: _insertReceipts,
            cancellation: cancellation);
    }

    private async Task ImportFull(
        string src,
        long startNumber,
        bool insertBodies,
        bool insertReceipts,
        Hash256 expectedHeader,
        CancellationToken cancellation)
    {
        var eraFiles = EraReader.GetAllEraFiles(src, _networkName, _fileSystem).ToArray();

        EraStore eraStore = new(eraFiles, _fileSystem);

        long startEpoch = startNumber / _epochSize;

        if (!eraStore.HasEpoch(startEpoch))
        {
            throw new EraImportException($"No {_networkName} epochs found for block {startNumber} in '{src}'");
        }

        DateTime lastProgress = DateTime.Now;
        long epochProcessed = 0;
        DateTime startTime = DateTime.Now;
        long txProcessed = 0;
        long totalblocks = 0;
        int blocksProcessed = 0;

        for (long i = startEpoch; eraStore.HasEpoch(i); i++)
        {
            using EraReader eraReader = await eraStore.GetReader(i, cancellation);

            await foreach ((Block b, TxReceipt[] r, UInt256 td) in eraReader)
            {
                cancellation.ThrowIfCancellationRequested();

                if (b.IsGenesis)
                {
                    continue;
                }
                if (b.Number < startNumber)
                {
                    continue;
                }
                if (b.Header.ParentHash != expectedHeader)
                {
                    throw new EraImportException($"Expected header '{expectedHeader}' in block number {b.Number} in Era1 archive '{eraStore.GetReaderPath(i)}', but got {b.Header.ParentHash}.");
                }

                if (insertBodies)
                {
                    if (b.IsBodyMissing)
                    {
                        throw new EraImportException($"Unexpected block without a body found in '{eraStore.GetReaderPath(i)}'. Archive might be corrupted.");
                    }

                    if (!_blockValidator.ValidateSuggestedBlock(b))
                    {
                        throw new EraImportException($"Era1 archive '{eraStore.GetReaderPath(i)}' contains an invalid block {b.ToString(Block.Format.Short)}.");
                    }
                }

                if (insertReceipts)
                {
                    ValidateReceipts(b, r);
                }

                cancellation.ThrowIfCancellationRequested();

                await SuggestBlock(b, r);

                expectedHeader = b.Header.Hash;

                blocksProcessed++;
                txProcessed += b.Transactions.Length;
                TimeSpan elapsed = DateTime.Now.Subtract(lastProgress);
                if (elapsed.TotalSeconds > TimeSpan.FromSeconds(10).TotalSeconds)
                {
                    ImportProgressChanged?.Invoke(this, new ImportProgressChangedArgs(DateTime.Now.Subtract(startTime), blocksProcessed, txProcessed, totalblocks, epochProcessed, eraStore.EpochCount));
                    lastProgress = DateTime.Now;
                }
            }
            epochProcessed++;
        }
        ImportProgressChanged?.Invoke(this, new ImportProgressChangedArgs(DateTime.Now.Subtract(startTime), blocksProcessed, txProcessed, totalblocks, epochProcessed, eraStore.EpochCount));
    }


    private async Task ImportBackfill(
    string src,
    long bodyBarrier,
    long receiptsBarrier,
    bool insertBodies,
    bool insertReceipts,
    CancellationToken cancellation)
    {
        var eraFiles = EraReader.GetAllEraFiles(src, _networkName, _fileSystem).ToArray();

        EraStore eraStore = new(eraFiles, _fileSystem);

        long startEpoch = eraStore.BiggestEpoch;

        DateTime lastProgress = DateTime.Now;
        long epochProcessed = 0;
        DateTime startTime = DateTime.Now;
        long txProcessed = 0;
        long totalblocks = 0;
        int blocksProcessed = 0;
        Hash256 expectedHeader = Keccak.Zero;

        for (long i = startEpoch; eraStore.HasEpoch(i); i--)
        {
            using EraReader eraReader = await eraStore.GetReader(i, true, cancellation);

            await foreach ((Block b, TxReceipt[] r, UInt256 td) in eraReader)
            {
                cancellation.ThrowIfCancellationRequested();

                if (b.IsGenesis)
                {
                    continue;
                }

                if (expectedHeader != Keccak.Zero && b.Header.Hash != expectedHeader)
                {
                    throw new EraImportException($"Expected header '{expectedHeader}' in block number {b.Number} in Era1 archive '{eraStore.GetReaderPath(i)}', but got {b.Header.ParentHash}.");
                }

                if (insertBodies)
                {
                    if (b.IsBodyMissing)
                    {
                        throw new EraImportException($"Unexpected block without a body found in '{eraStore.GetReaderPath(i)}'. Archive might be corrupted.");
                    }

                    if (!BlockValidator.ValidateBodyAgainstHeader(b.Header, b.Body))
                    {
                        throw new EraImportException($"Era1 archive '{eraStore.GetReaderPath(i)}' contains an invalid block {b.ToString(Block.Format.Short)}.");
                    }
                }

                if (insertReceipts)
                {
                    ValidateReceipts(b, r);
                }

                cancellation.ThrowIfCancellationRequested();

                if (b.Header.TotalDifficulty == null)
                    b.Header.TotalDifficulty = td;

                InsertInBlockTree(b, r, insertBodies, insertReceipts, bodyBarrier, receiptsBarrier);

                expectedHeader = b.Header.ParentHash;

                blocksProcessed++;
                txProcessed += b.Transactions.Length;
                TimeSpan elapsed = DateTime.Now.Subtract(lastProgress);
                if (elapsed.TotalSeconds > TimeSpan.FromSeconds(10).TotalSeconds)
                {
                    ImportProgressChanged?.Invoke(this, new ImportProgressChangedArgs(DateTime.Now.Subtract(startTime), blocksProcessed, txProcessed, totalblocks, epochProcessed, eraStore.EpochCount));
                    lastProgress = DateTime.Now;
                }
            }
            epochProcessed++;
        }
        ImportProgressChanged?.Invoke(this, new ImportProgressChangedArgs(DateTime.Now.Subtract(startTime), blocksProcessed, txProcessed, totalblocks, epochProcessed, eraStore.EpochCount));
    }

    private void InsertInBlockTree(Block b, TxReceipt[] r, bool insertBodies, bool insertReceipts, long bodyBarrier, long receiptBarrier)
    {
        InsertHeader(b.Header);

        if (insertBodies && bodyBarrier < b.Number)
        {
            InsertBlock(b);
            _blockTree.LowestInsertedBodyNumber = b.Number;
        }
        if (insertReceipts && receiptBarrier < b.Number)
        {
            InsertReceipts(b, r);
        }
    }

    private void InsertHeader(BlockHeader header)
    {
        AddBlockResult result = _blockTree.Insert(header, BlockTreeInsertHeaderOptions.TotalDifficultyNotNeeded);
        EnsureAddResult(result, typeof(BlockHeader));
    }
    private void InsertBlock(Block block)
    {
        AddBlockResult result = _blockTree.Insert(block, BlockTreeInsertBlockOptions.SkipCanAcceptNewBlocks, bodiesWriteFlags: WriteFlags.DisableWAL);
        EnsureAddResult(result, typeof(BlockBody));
    }

    private void InsertReceipts(Block block, TxReceipt[] receipts)
    {
        _receiptStorage.Insert(block, receipts, true);
    }

    private async Task SuggestBlock(Block block, TxReceipt[] receipts)
    {
        var options = BlockTreeSuggestOptions.ShouldProcess;
        var addResult = await _blockTree.SuggestBlockAsync(block, options);
        EnsureAddResult(addResult, typeof(Block));
    }
    private void EnsureAddResult(AddBlockResult result, Type added)
    {
        switch (result)
        {
            case AddBlockResult.CannotAccept:
                throw new EraImportException($"Rejected {added.Name} in Era1 archive");
            case AddBlockResult.UnknownParent:
                throw new EraImportException($"Unknown parent for {added.Name} in Era1 archive");
            case AddBlockResult.InvalidBlock:
                throw new EraImportException("Invalid block in Era1 archive");
            case AddBlockResult.AlreadyKnown:
            case AddBlockResult.Added:
                break;
            default:
                throw new NotSupportedException($"Not supported value of {nameof(AddBlockResult)} = {result}");
        }
    }

    private void ValidateReceipts(Block block, TxReceipt[] blockReceipts)
    {
        Hash256 receiptsRoot = new ReceiptTrie(_specProvider.GetSpec(block.Header), blockReceipts).RootHash;

        if (receiptsRoot != block.ReceiptsRoot)
        {
            throw new EraImportException($"Wrong receipts root in Era1 archive for block {block.ToString(Block.Format.Short)}.");
        }
    }
}
