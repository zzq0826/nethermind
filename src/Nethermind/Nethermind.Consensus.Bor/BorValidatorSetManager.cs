using Nethermind.Core;

namespace Nethermind.Consensus.Bor;

public class BorValidatorSetManager : IBorValidatorSetManager
{
    private readonly IHeimdallClient _heimdallClient;
    private readonly IBorParamsHelper _borHelper;
    private readonly IBorValidatorSetContract _validatorSetContract;

    public BorValidatorSetManager(
        IHeimdallClient heimdallClient,
        IBorParamsHelper borHelper,
        IBorValidatorSetContract validatorSetContract)
    {
        _heimdallClient = heimdallClient;
        _borHelper = borHelper;
        _validatorSetContract = validatorSetContract;
    }


    public void ProcessBlock(BlockHeader header)
    {
        throw new NotImplementedException();
    }
}