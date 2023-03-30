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

        System.Console.WriteLine($"Processing block {number} for state sync");

        long sprintSize = _borHelper.CalculateSprintSize(number);

        BlockHeader snapshotHeader = _blockTree.FindHeader(number - 1)!;

        ulong fromId = _stateReceiverContract.LastStateId(snapshotHeader) + 1;
        ulong toTime = _blockTree.FindHeader(number - sprintSize)!.Timestamp;

        _logger.Info($"Fetching state updates from Heimdall for block {number} from `{fromId}` to `{toTime}`");
        StateSyncEventRecord[] eventRecords = _heimdallClient.StateSyncEvents(fromId, toTime);

        // TODO: override state sync event records

        foreach (StateSyncEventRecord eventRecord in eventRecords)
        {
            if (eventRecord.Id < fromId)
                continue;

            // TODO: validate event record

            _stateReceiverContract.CommitState(header, eventRecord);
        }
    }
}
