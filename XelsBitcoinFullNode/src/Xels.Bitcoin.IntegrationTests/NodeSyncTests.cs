using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Xels.Bitcoin.Connection;
using Xels.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace Xels.Bitcoin.IntegrationTests
{
    public class NodeSyncTests
    {
        public NodeSyncTests()
        {
            // These tests are for mostly for POW. Set the flags to the expected values.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }

        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node1 = builder.CreateXelsPowNode();
                var node2 = builder.CreateXelsPowNode();
                builder.StartAll();
                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedPeers);
                var rpc1 = node1.CreateRPCClient();
                rpc1.AddNode(node2.Endpoint, true);
                Assert.Single(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Single(node2.FullNode.ConnectionManager.ConnectedPeers);

                var behavior = node1.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.False(behavior.Inbound);
                Assert.True(behavior.OneTry);
                behavior = node2.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.True(behavior.Inbound);
                Assert.False(behavior.OneTry);
            }
        }

        [Fact]
        public void CanXelsSyncFromCore()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsNode = builder.CreateXelsPowNode();
                var coreNode = builder.CreateNode();
                builder.StartAll();

                xelsNode.NotInIBD();

                var tip = coreNode.FindBlock(10).Last();
                xelsNode.CreateRPCClient().AddNode(coreNode.Endpoint, true);
                TestHelper.WaitLoop(() => xelsNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());
                var bestBlockHash = xelsNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                //Now check if Core connect to xels
                xelsNode.CreateRPCClient().RemoveNode(coreNode.Endpoint);
                TestHelper.WaitLoop(() => coreNode.CreateRPCClient().GetPeersInfo().Length == 0);

                tip = coreNode.FindBlock(10).Last();
                coreNode.CreateRPCClient().AddNode(xelsNode.Endpoint, true);
                TestHelper.WaitLoop(() => xelsNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = xelsNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void CanXelsSyncFromXels()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsNode = builder.CreateXelsPowNode();
                var xelsNodeSync = builder.CreateXelsPowNode();
                var coreCreateNode = builder.CreateNode();
                builder.StartAll();

                xelsNode.NotInIBD();
                xelsNodeSync.NotInIBD();

                // first seed a core node with blocks and sync them to a xels node
                // and wait till the xels node is fully synced
                var tip = coreCreateNode.FindBlock(5).Last();
                xelsNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                TestHelper.WaitLoop(() => xelsNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
                var bestBlockHash = xelsNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new xels node which will download
                // the blocks using the GetData payload
                xelsNodeSync.CreateRPCClient().AddNode(xelsNode.Endpoint, true);

                // wait for download and assert
                TestHelper.WaitLoop(() => xelsNode.CreateRPCClient().GetBestBlockHash() == xelsNodeSync.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = xelsNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void CanCoreSyncFromXels()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsNode = builder.CreateXelsPowNode();
                var coreNodeSync = builder.CreateNode();
                var coreCreateNode = builder.CreateNode();
                builder.StartAll();

                xelsNode.NotInIBD();

                // first seed a core node with blocks and sync them to a xels node
                // and wait till the xels node is fully synced
                var tip = coreCreateNode.FindBlock(5).Last();
                xelsNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                TestHelper.WaitLoop(() => xelsNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => xelsNode.FullNode.HighestPersistedBlock().HashBlock == xelsNode.FullNode.Chain.Tip.HashBlock);

                var bestBlockHash = xelsNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new xels node which will download
                // the blocks using the GetData payload
                coreNodeSync.CreateRPCClient().AddNode(xelsNode.Endpoint, true);

                // wait for download and assert
                TestHelper.WaitLoop(() => xelsNode.CreateRPCClient().GetBestBlockHash() == coreNodeSync.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = coreNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void Given__NodesAreSynced__When__ABigReorgHappens__Then__TheReorgIsIgnored()
        {
            // Temporary fix so the Network static initialize will not break.
            var m = Network.Main;
            Transaction.TimeStamp = true;
            Block.BlockSignature = true;
            try
            {
                using (NodeBuilder builder = NodeBuilder.Create())
                {
                    var xelsMiner = builder.CreateXelsPosNode();
                    var xelsSyncer = builder.CreateXelsPosNode();
                    var xelsReorg = builder.CreateXelsPosNode();

                    builder.StartAll();
                    xelsMiner.NotInIBD();
                    xelsSyncer.NotInIBD();
                    xelsReorg.NotInIBD();

                    // TODO: set the max allowed reorg threshold here
                    // assume a reorg of 10 blocks is not allowed.
                    xelsMiner.FullNode.ChainBehaviorState.MaxReorgLength = 10;
                    xelsSyncer.FullNode.ChainBehaviorState.MaxReorgLength = 10;
                    xelsReorg.FullNode.ChainBehaviorState.MaxReorgLength = 10;

                    xelsMiner.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsMiner.FullNode.Network));
                    xelsReorg.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsReorg.FullNode.Network));

                    xelsMiner.GenerateXelsWithMiner(1);

                    // wait for block repo for block sync to work
                    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsMiner));
                    xelsMiner.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                    xelsMiner.CreateRPCClient().AddNode(xelsSyncer.Endpoint, true);

                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsMiner, xelsSyncer));
                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsMiner, xelsReorg));

                    // create a reorg by mining on two different chains
                    // ================================================

                    xelsMiner.CreateRPCClient().RemoveNode(xelsReorg.Endpoint);
                    xelsSyncer.CreateRPCClient().RemoveNode(xelsReorg.Endpoint);
                    TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(xelsReorg));

                    var t1 = Task.Run(() => xelsMiner.GenerateXelsWithMiner(11));
                    var t2 = Task.Delay(1000).ContinueWith(t => xelsReorg.GenerateXelsWithMiner(12));
                    Task.WaitAll(t1, t2);
                    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsMiner));
                    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsReorg));

                    // make sure the nodes are actually on different chains.
                    Assert.NotEqual(xelsMiner.FullNode.Chain.GetBlock(2).HashBlock, xelsReorg.FullNode.Chain.GetBlock(2).HashBlock);

                    TestHelper.TriggerSync(xelsSyncer);
                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsMiner, xelsSyncer));

                    // The hash before the reorg node is connected.
                    var hashBeforeReorg = xelsMiner.FullNode.Chain.Tip.HashBlock;

                    // connect the reorg chain
                    xelsMiner.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                    xelsSyncer.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);

                    // trigger nodes to sync
                    TestHelper.TriggerSync(xelsMiner);
                    TestHelper.TriggerSync(xelsReorg);
                    TestHelper.TriggerSync(xelsSyncer);

                    // wait for the synced chain to get headers updated.
                    TestHelper.WaitLoop(() => !xelsReorg.FullNode.ConnectionManager.ConnectedPeers.Any());

                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsMiner, xelsSyncer));
                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReorg, xelsMiner) == false);
                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReorg, xelsSyncer) == false);

                    // check that a reorg did not happen.
                    Assert.Equal(hashBeforeReorg, xelsSyncer.FullNode.Chain.Tip.HashBlock);
                }
            }
            finally
            {
                Transaction.TimeStamp = false;
                Block.BlockSignature = false;
            }
        }

        /// <summary>
        /// This tests simulates scenario 2 from issue 636.
        /// <para>
        /// The test mines a block and roughly at the same time, but just after that, a new block at the same height
        /// arrives from the puller. Then another block comes from the puller extending the chain without the block we mined.
        /// </para>
        /// </summary>
        /// <seealso cref="https://github.com/xelsproject/XelsBitcoinFullNode/issues/636"/>
        [Fact]
        public void PullerVsMinerRaceCondition()
        {
            // Temporary fix so the Network static initialize will not break.
            var m = Network.Main;
            Transaction.TimeStamp = true;
            Block.BlockSignature = true;
            try
            {
                using (NodeBuilder builder = NodeBuilder.Create())
                {
                    // This represents local node.
                    var xelsMinerLocal = builder.CreateXelsPosNode();

                    // This represents remote, which blocks are received by local node using its puller.
                    var xelsMinerRemote = builder.CreateXelsPosNode();

                    builder.StartAll();
                    xelsMinerLocal.NotInIBD();
                    xelsMinerRemote.NotInIBD();

                    xelsMinerLocal.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsMinerLocal.FullNode.Network));
                    xelsMinerRemote.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsMinerRemote.FullNode.Network));

                    // Let's mine block Ap and Bp.
                    xelsMinerRemote.GenerateXelsWithMiner(2);

                    // Wait for block repository for block sync to work.
                    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsMinerRemote));
                    xelsMinerLocal.CreateRPCClient().AddNode(xelsMinerRemote.Endpoint, true);

                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsMinerLocal, xelsMinerRemote));

                    // Now disconnect the peers and mine block C2p on remote.
                    xelsMinerLocal.CreateRPCClient().RemoveNode(xelsMinerRemote.Endpoint);
                    TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(xelsMinerRemote));

                    // Mine block C2p.
                    xelsMinerRemote.GenerateXelsWithMiner(1);
                    Thread.Sleep(2000);

                    // Now reconnect nodes and mine block C1s before C2p arrives.
                    xelsMinerLocal.CreateRPCClient().AddNode(xelsMinerRemote.Endpoint, true);
                    xelsMinerLocal.GenerateXelsWithMiner(1);

                    // Mine block Dp.
                    uint256 dpHash = xelsMinerRemote.GenerateXelsWithMiner(1)[0];

                    // Now we wait until the local node's chain tip has correct hash of Dp.
                    TestHelper.WaitLoop(() => xelsMinerLocal.FullNode.Chain.Tip.HashBlock.Equals(dpHash));

                    // Then give it time to receive the block from the puller.
                    Thread.Sleep(2500);

                    // Check that local node accepted the Dp as consensus tip.
                    Assert.Equal(xelsMinerLocal.FullNode.ChainBehaviorState.ConsensusTip.HashBlock, dpHash);
                }
            }
            finally
            {
                Transaction.TimeStamp = false;
                Block.BlockSignature = false;
            }
        }

        /// <summary>
        /// This test simulates scenario from issue #862.
        /// <para>
        /// Connection scheme:
        /// Network - Node1 - MiningNode
        /// </para>
        /// </summary>
        [Fact]
        public void MiningNodeWithOneConnectionAlwaysSynced()
        {
            NetworkSimulator simulator = new NetworkSimulator();

            simulator.Initialize(4);

            var miner = simulator.Nodes[0];
            var connector = simulator.Nodes[1];
            var networkNode1 = simulator.Nodes[2];
            var networkNode2 = simulator.Nodes[3];

            // Connect nodes with each other. Miner is connected to connector and connector, node1, node2 are connected with each other.
            miner.CreateRPCClient().AddNode(connector.Endpoint, true);
            connector.CreateRPCClient().AddNode(networkNode1.Endpoint, true);
            connector.CreateRPCClient().AddNode(networkNode2.Endpoint, true);
            networkNode1.CreateRPCClient().AddNode(networkNode2.Endpoint, true);

            simulator.MakeSureEachNodeCanMineAndSync();

            int networkHeight = miner.FullNode.Chain.Height;
            Assert.Equal(networkHeight, simulator.Nodes.Count);

            // Random node on network generates a block.
            networkNode1.GenerateXels(1);

            // Wait until connector get the hash of network's block.
            while ((connector.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != networkNode1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) ||
                   (networkNode1.FullNode.ChainBehaviorState.ConsensusTip.Height == networkHeight))
                Thread.Sleep(1);

            // Make sure that miner did not advance yet but connector did.
            Assert.NotEqual(miner.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(connector.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(miner.FullNode.Chain.Tip.Height, networkHeight);
            Assert.Equal(connector.FullNode.Chain.Tip.Height, networkHeight+1);

            // Miner mines the block.
            miner.GenerateXels(1);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(miner));

            networkHeight++;

            // Make sure that at this moment miner's tip != network's and connector's tip.
            Assert.NotEqual(miner.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(connector.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(miner.FullNode.Chain.Tip.Height, networkHeight);
            Assert.Equal(connector.FullNode.Chain.Tip.Height, networkHeight);

            connector.GenerateXels(1);
            networkHeight++;

            int delay = 0;

            while (true)
            {
                Thread.Sleep(50);
                if (simulator.DidAllNodesReachHeight(networkHeight))
                    break;
                delay += 50;

                Assert.True(delay < 10 * 1000, "Miner node was not able to advance!");
            }

            Assert.Equal(networkNode1.FullNode.Chain.Tip.HashBlock, miner.FullNode.Chain.Tip.HashBlock);
        }
    }
}
