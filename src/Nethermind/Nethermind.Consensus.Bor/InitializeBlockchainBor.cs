using Nethermind.Api;
using Nethermind.Consensus.Processing;
using Nethermind.Init.Steps;

namespace Nethermind.Consensus.Bor;

public class InitializeBlockchainBor : InitializeBlockchain
{
    private readonly BorBlockProcessorFactory _blockProcessorFactory;

    public InitializeBlockchainBor(INethermindApi api) : base(api)
    {
        _blockProcessorFactory = new(api);
    }

    protected override IBlockProcessor CreateBlockProcessor()
    {
        return _blockProcessorFactory.Create();
    }
}