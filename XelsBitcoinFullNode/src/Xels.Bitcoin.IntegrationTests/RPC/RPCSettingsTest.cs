using NBitcoin;
using Xels.Bitcoin.Builder;
using Xels.Bitcoin.Configuration;
using Xels.Bitcoin.Features.Consensus;
using Xels.Bitcoin.Features.RPC;
using Xels.Bitcoin.Tests;
using Xunit;

namespace Xels.Bitcoin.IntegrationTests.RPC
{
    public class RPCSettingsTest : TestBase
    {
        [Fact]
        public void CanSpecifyRPCSettings()
        {
            var initialBlockSignature = Block.BlockSignature;

            try
            {
                Block.BlockSignature = false;
                var dir = CreateTestDir(this);

                NodeSettings nodeSettings = new NodeSettings(args:new string[] { $"-datadir={dir}" });

                var node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UsePowConsensus()
                    .AddRPC(x =>
                    {
                        x.RpcUser = "abc";
                        x.RpcPassword = "def";
                        x.RPCPort = 91;
                    })
                    .Build();

                var settings = node.NodeService<RpcSettings>();

                settings.Load(nodeSettings);

                Assert.Equal("abc", settings.RpcUser);
                Assert.Equal("def", settings.RpcPassword);
                Assert.Equal(91, settings.RPCPort);
            }
            finally
            {
                Block.BlockSignature = initialBlockSignature;
            }

        }
    }
}
