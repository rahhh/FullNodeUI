using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Xels.Bitcoin.Connection;
using Xels.Bitcoin.Features.BlockStore;
using Xels.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xels.Bitcoin.Utilities;
using Xunit;

namespace Xels.Bitcoin.IntegrationTests
{
    public class BlockStoreTests
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes logger factory for tests in this class.
        /// </summary>
        public BlockStoreTests()
        {
            // These tests use Network.Main.
            // Ensure that these static flags have the expected values.
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;

            this.loggerFactory = new LoggerFactory();
            DBreezeSerializer serializer = new DBreezeSerializer();
            serializer.Initialize();
        }

        private void BlockRepositoryBench()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, DateTimeProvider.Default, this.loggerFactory))
                {
                    var lst = new List<Block>();
                    for (int i = 0; i < 30; i++)
                    {
                        // roughly 1mb blocks
                        var block = new Block();
                        for (int j = 0; j < 3000; j++)
                        {
                            var trx = new Transaction();
                            block.AddTransaction(new Transaction());
                            trx.AddInput(new TxIn(Script.Empty));
                            trx.AddOutput(Money.COIN + j + i, new Script(Guid.NewGuid().ToByteArray()
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())));
                            trx.AddInput(new TxIn(Script.Empty));
                            trx.AddOutput(Money.COIN + j + i + 1, new Script(Guid.NewGuid().ToByteArray()
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())));
                            block.AddTransaction(trx);
                        }
                        block.UpdateMerkleRoot();
                        block.Header.HashPrevBlock = lst.Any() ? lst.Last().GetHash() : Network.Main.GenesisHash;
                        lst.Add(block);
                    }

                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();
                    var first = stopwatch.ElapsedMilliseconds;
                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();
                    var second = stopwatch.ElapsedMilliseconds;
                }
            }
        }

        [Fact]
        public void BlockRepositoryPutBatch()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, DateTimeProvider.Default, this.loggerFactory))
                {
                    blockRepo.SetTxIndexAsync(true).Wait();

                    var lst = new List<Block>();
                    for (int i = 0; i < 5; i++)
                    {
                        // put
                        var block = new Block();
                        block.AddTransaction(new Transaction());
                        block.AddTransaction(new Transaction());
                        block.Transactions[0].AddInput(new TxIn(Script.Empty));
                        block.Transactions[0].AddOutput(Money.COIN + i * 2, Script.Empty);
                        block.Transactions[1].AddInput(new TxIn(Script.Empty));
                        block.Transactions[1].AddOutput(Money.COIN + i * 2 + 1, Script.Empty);
                        block.UpdateMerkleRoot();
                        block.Header.HashPrevBlock = lst.Any() ? lst.Last().GetHash() : Network.Main.GenesisHash;
                        lst.Add(block);
                    }

                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();

                    // check each block
                    foreach (var block in lst)
                    {
                        var received = blockRepo.GetAsync(block.GetHash()).GetAwaiter().GetResult();
                        Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));

                        foreach (var transaction in block.Transactions)
                        {
                            var trx = blockRepo.GetTrxAsync(transaction.GetHash()).GetAwaiter().GetResult();
                            Assert.True(trx.ToBytes().SequenceEqual(transaction.ToBytes()));
                        }
                    }

                    // delete
                    blockRepo.DeleteAsync(lst.ElementAt(2).GetHash(), new[] { lst.ElementAt(2).GetHash() }.ToList()).GetAwaiter().GetResult();
                    var deleted = blockRepo.GetAsync(lst.ElementAt(2).GetHash()).GetAwaiter().GetResult();
                    Assert.Null(deleted);
                }
            }
        }

        [Fact]
        public void BlockRepositoryBlockHash()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, DateTimeProvider.Default, this.loggerFactory))
                {
                    blockRepo.InitializeAsync().GetAwaiter().GetResult();

                    Assert.Equal(Network.Main.GenesisHash, blockRepo.BlockHash);
                    var hash = new Block().GetHash();
                    blockRepo.SetBlockHashAsync(hash).GetAwaiter().GetResult();
                    Assert.Equal(hash, blockRepo.BlockHash);
                }
            }
        }

        [Fact]
        public void BlockBroadcastInv()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsNodeSync = builder.CreateXelsPowNode();
                var xelsNode1 = builder.CreateXelsPowNode();
                var xelsNode2 = builder.CreateXelsPowNode();
                builder.StartAll();
                xelsNodeSync.NotInIBD();
                xelsNode1.NotInIBD();
                xelsNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                xelsNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsNodeSync.FullNode.Network));
                xelsNodeSync.GenerateXelsWithMiner(10); // coinbase maturity = 10
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => xelsNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == xelsNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => xelsNodeSync.FullNode.ChainBehaviorState.ConsensusTip.HashBlock == xelsNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => xelsNodeSync.FullNode.HighestPersistedBlock().HashBlock == xelsNodeSync.FullNode.Chain.Tip.HashBlock);

                // sync both nodes
                xelsNode1.CreateRPCClient().AddNode(xelsNodeSync.Endpoint, true);
                xelsNode2.CreateRPCClient().AddNode(xelsNodeSync.Endpoint, true);
                TestHelper.WaitLoop(() => xelsNode1.CreateRPCClient().GetBestBlockHash() == xelsNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => xelsNode2.CreateRPCClient().GetBestBlockHash() == xelsNodeSync.CreateRPCClient().GetBestBlockHash());

                // set node2 to use inv (not headers)
                xelsNode2.FullNode.ConnectionManager.ConnectedPeers.First().Behavior<BlockStoreBehavior>().PreferHeaders = false;

                // generate two new blocks
                xelsNodeSync.GenerateXelsWithMiner(2);
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => xelsNodeSync.FullNode.Chain.Tip.HashBlock == xelsNodeSync.FullNode.ConsensusLoop().Tip.HashBlock);
                TestHelper.WaitLoop(() => xelsNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(xelsNodeSync.CreateRPCClient().GetBestBlockHash()).Result != null);

                // wait for the other nodes to pick up the newly generated blocks
                TestHelper.WaitLoop(() => xelsNode1.CreateRPCClient().GetBestBlockHash() == xelsNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => xelsNode2.CreateRPCClient().GetBestBlockHash() == xelsNodeSync.CreateRPCClient().GetBestBlockHash());
            }
        }

        [Fact]
        public void BlockStoreCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsNodeSync = builder.CreateXelsPowNode();
                builder.StartAll();
                xelsNodeSync.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                xelsNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsNodeSync.FullNode.Network));

                xelsNodeSync.GenerateXelsWithMiner(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsNodeSync));

                // set the tip of best chain some blocks in the apst
                xelsNodeSync.FullNode.Chain.SetTip(xelsNodeSync.FullNode.Chain.GetBlock(xelsNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                xelsNodeSync.FullNode.Dispose();

                var newNodeInstance = builder.CloneXelsNode(xelsNodeSync);

                // load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // check that store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.HighestPersistedBlock().HashBlock);
                //TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsNodeSync));
            }
        }

        [Fact]
        public void BlockStoreCanReorg()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsNodeSync = builder.CreateXelsPowNode();
                var xelsNode1 = builder.CreateXelsPowNode();
                var xelsNode2 = builder.CreateXelsPowNode();
                builder.StartAll();
                xelsNodeSync.NotInIBD();
                xelsNode1.NotInIBD();
                xelsNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                xelsNode1.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsNodeSync.FullNode.Network));
                xelsNode2.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsNodeSync.FullNode.Network));
                // sync both nodes
                xelsNodeSync.CreateRPCClient().AddNode(xelsNode1.Endpoint, true);
                xelsNodeSync.CreateRPCClient().AddNode(xelsNode2.Endpoint, true);

                xelsNode1.GenerateXelsWithMiner(10);
                TestHelper.WaitLoop(() => xelsNode1.FullNode.HighestPersistedBlock().Height == 10);

                TestHelper.WaitLoop(() => xelsNode1.FullNode.HighestPersistedBlock().HashBlock == xelsNodeSync.FullNode.HighestPersistedBlock().HashBlock);
                TestHelper.WaitLoop(() => xelsNode2.FullNode.HighestPersistedBlock().HashBlock == xelsNodeSync.FullNode.HighestPersistedBlock().HashBlock);

                // remove node 2
                xelsNodeSync.CreateRPCClient().RemoveNode(xelsNode2.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(xelsNode2));

                // mine some more with node 1
                xelsNode1.GenerateXelsWithMiner(10);

                // wait for node 1 to sync
                TestHelper.WaitLoop(() => xelsNode1.FullNode.HighestPersistedBlock().Height == 20);
                TestHelper.WaitLoop(() => xelsNode1.FullNode.HighestPersistedBlock().HashBlock == xelsNodeSync.FullNode.HighestPersistedBlock().HashBlock);

                // remove node 1
                xelsNodeSync.CreateRPCClient().RemoveNode(xelsNode1.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(xelsNode1));

                // mine a higher chain with node2
                xelsNode2.GenerateXelsWithMiner(20);
                TestHelper.WaitLoop(() => xelsNode2.FullNode.HighestPersistedBlock().Height == 30);

                // add node2
                xelsNodeSync.CreateRPCClient().AddNode(xelsNode2.Endpoint, true);

                // node2 should be synced
                TestHelper.WaitLoop(() => xelsNode2.FullNode.HighestPersistedBlock().HashBlock == xelsNodeSync.FullNode.HighestPersistedBlock().HashBlock);
            }
        }

        [Fact]
        public void BlockStoreIndexTx()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsNode1 = builder.CreateXelsPowNode();
                var xelsNode2 = builder.CreateXelsPowNode();
                builder.StartAll();
                xelsNode1.NotInIBD();
                xelsNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                xelsNode1.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsNode1.FullNode.Network));
                xelsNode2.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsNode2.FullNode.Network));
                // sync both nodes
                xelsNode1.CreateRPCClient().AddNode(xelsNode2.Endpoint, true);
                xelsNode1.GenerateXelsWithMiner(10);
                TestHelper.WaitLoop(() => xelsNode1.FullNode.HighestPersistedBlock().Height == 10);
                TestHelper.WaitLoop(() => xelsNode1.FullNode.HighestPersistedBlock().HashBlock == xelsNode2.FullNode.HighestPersistedBlock().HashBlock);

                var bestBlock1 = xelsNode1.FullNode.BlockStoreManager().BlockRepository.GetAsync(xelsNode1.FullNode.Chain.Tip.HashBlock).Result;
                Assert.NotNull(bestBlock1);

                // get the block coinbase trx
                var trx = xelsNode2.FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(bestBlock1.Transactions.First().GetHash()).Result;
                Assert.NotNull(trx);
                Assert.Equal(bestBlock1.Transactions.First().GetHash(), trx.GetHash());
            }
        }
    }
}
