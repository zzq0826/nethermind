using System.Diagnostics.Eventing.Reader;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Consensus.Bor;

public class BorStateSyncManager : IBorStateSyncManager
{
    private readonly ILogger _logger;
    private readonly IBlockTree _blockTree;
    private readonly IBorParamsHelper _borHelper;
    private readonly IHeimdallClient _heimdallClient;
    private readonly IBorStateReceiverContract _stateReceiverContract;

    public BorStateSyncManager(
        ILogManager logManager,
        IBlockTree blockTree,
        IBorParamsHelper borHelper,
        IHeimdallClient heimdallClient,
        IBorStateReceiverContract stateReceiverContract)
    {
        _logger = logManager.GetClassLogger();
        _blockTree = blockTree;
        _borHelper = borHelper;
        _heimdallClient = heimdallClient;
        _stateReceiverContract = stateReceiverContract;
    }

    public void ProcessBlock(BlockHeader header)
    {
        long number = header.Number;
        if (!_borHelper.IsSprintStart(number))
            return;

        long sprintSize = _borHelper.CalculateSprintSize(number);

        BlockHeader snapshotHeader = _blockTree.FindHeader(number - 1)!;

        ulong fromId = _stateReceiverContract.LastStateId(snapshotHeader) + 1;
        ulong toTime = _blockTree.FindHeader(number - sprintSize)!.Timestamp;

        if (_logger.IsDebug)
            _logger.Debug($"Fetching state updates from Heimdall for block {number} from `{fromId}` to `{toTime}`");

        StateSyncEventRecord[] eventRecords = _heimdallClient.StateSyncEvents(fromId, toTime);

        // TODO: override state sync event records

        if (_logger.IsInfo && eventRecords.Length > 0)
            _logger.Info($"Applying state updates from Heimdall for block {number} ({eventRecords.Length} events)");

        foreach (StateSyncEventRecord eventRecord in eventRecords)
        {
            if (eventRecord.Id < fromId)
                continue;

            // TODO: validate event record

            _stateReceiverContract.CommitState(header, eventRecord);
        }
    }
}
