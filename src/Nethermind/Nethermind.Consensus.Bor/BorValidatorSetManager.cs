using Nethermind.Abi;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Consensus.Bor;

public class BorValidatorSetManager : IBorValidatorSetManager
{
    private readonly ILogger _logger;

    private readonly ulong _chainId;
    private readonly IBlockTree _blockTree;
    private readonly IHeimdallClient _heimdallClient;
    private readonly IBorParamsHelper _borHelper;
    private readonly IBorValidatorSetContract _validatorSetContract;

    public BorValidatorSetManager(
        ILogManager logManager,
        ulong chainId,
        IBlockTree blockTree,
        IHeimdallClient heimdallClient,
        IBorParamsHelper borHelper,
        IBorValidatorSetContract validatorSetContract)
    {
        _logger = logManager.GetClassLogger();

        _chainId = chainId;
        _blockTree = blockTree;
        _heimdallClient = heimdallClient;
        _borHelper = borHelper;
        _validatorSetContract = validatorSetContract;
    }


    public void ProcessBlock(BlockHeader header)
    {
        if (!_borHelper.IsSprintStart(header.Number))
            return;

        BlockHeader parentHeader = _blockTree.FindParentHeader(header, BlockTreeLookupOptions.None)!;
        BorSpan currSpan = _validatorSetContract.GetCurrentSpan(parentHeader);

        if (!ShouldCommitNextSpan(currSpan.EndBlock, header.Number))
            return;

        // Fetch the next span from Heimdall
        HeimdallSpan nextSpan = _heimdallClient.GetSpan(currSpan.Number + 1);

        // I'm adding this check here because Bor has it, but it doesn't make sense imho
        if (nextSpan.ChainId != _chainId)
            throw new Exception("Heimdall and Bor chain ids differ from each other");

        if (_logger.IsInfo)
            _logger.Info($"Committing span {nextSpan.Number} to validator set contract at block {header.Number}");

        try
        {
            // Commit the span we got from Heimdall to the validator set contract
            _validatorSetContract.CommitSpan(header, nextSpan);
        }
        catch (AbiException e)
        {
            if (_logger.IsWarn)
                _logger.Warn($"Ignoring span commit error to match Bor behavior: {e.Message}");
        }
    }

    private bool ShouldCommitNextSpan(long spanEndBlock, long headerNumber)
    {
        // check span is not set initially
        if (spanEndBlock == 0)
            return true;

        long sprintSize = _borHelper.CalculateSprintSize(headerNumber);

        // if current block is first block of last sprint in current span
        return spanEndBlock > sprintSize && spanEndBlock - sprintSize + 1 == headerNumber;
    }
}