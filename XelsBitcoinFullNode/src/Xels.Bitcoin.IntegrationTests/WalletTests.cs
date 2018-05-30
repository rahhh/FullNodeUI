using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xels.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Xels.Bitcoin.Features.Consensus;
using Xels.Bitcoin.Features.Wallet;
using Xels.Bitcoin.Features.Wallet.Controllers;
using Xels.Bitcoin.Features.Wallet.Interfaces;
using Xels.Bitcoin.Features.Wallet.Models;
using Xunit;
using System;
using Xels.Bitcoin.Utilities;
using System.Text;

namespace Xels.Bitcoin.IntegrationTests
{
    public class WalletTests : IDisposable
    {
        private bool initialBlockSignature;
        public WalletTests()
        {
            this.initialBlockSignature = Block.BlockSignature;
            Block.BlockSignature = false;
        }

        public void Dispose()
        {
            Block.BlockSignature = this.initialBlockSignature;
        }

        [Fact]
        public void WalletCanReceiveAndSendCorrectly()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsSender = builder.CreateXelsPowNode();
                var xelsReceiver = builder.CreateXelsPowNode();

                builder.StartAll();
                xelsSender.NotInIBD();
                xelsReceiver.NotInIBD();

                // get a key from the wallet
                var mnemonic1 = xelsSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                var mnemonic2 = xelsReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = xelsSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = xelsSender.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                xelsSender.SetDummyMinerSecret(new BitcoinSecret(key, xelsSender.FullNode.Network));
                var maturity = (int)xelsSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                xelsSender.GenerateXels(maturity + 5);
                // wait for block repo for block sync to work

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));

                // the mining should add coins to the wallet
                var total = xelsSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 105 * 50, total);

                // sync both nodes
                xelsSender.CreateRPCClient().AddNode(xelsReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));

                // send coins to the receiver
                var sendto = xelsReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var trx = xelsSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(
                    new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

                // broadcast to the other node
                xelsSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => xelsReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.WaitLoop(() => xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

                var receivetotal = xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // generate two new blocks do the trx is confirmed
                xelsSender.GenerateXels(1, new List<Transaction>(new[] { trx.Clone() }));
                xelsSender.GenerateXels(1);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));

                TestHelper.WaitLoop(() => maturity + 6 == xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);
            }
        }

        [Fact]
        public void WalletCanSendOneTransactionWithManyOutputs()
        { 
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode xelsSender = builder.CreateXelsPowNode();
                CoreNode xelsReceiver = builder.CreateXelsPowNode();

                builder.StartAll();
                xelsSender.NotInIBD();
                xelsReceiver.NotInIBD();

                xelsSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                xelsReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");

                HdAddress addr = xelsSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                Wallet wallet = xelsSender.FullNode.WalletManager().GetWalletByName("mywallet");
                Key key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                xelsSender.SetDummyMinerSecret(new BitcoinSecret(key, xelsSender.FullNode.Network));
                int maturity = (int)xelsSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                xelsSender.GenerateXelsWithMiner(maturity + 51);

                // Wait for block repo for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                
                Assert.Equal(Money.COIN * 150 * 50, xelsSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount));

                // Sync both nodes.
                xelsSender.CreateRPCClient().AddNode(xelsReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));

                // Get 50 unused addresses from the receiver.
                IEnumerable<HdAddress> recevierAddresses = xelsReceiver.FullNode.WalletManager()
                    .GetUnusedAddresses(new WalletAccountReference("mywallet", "account 0"), 50);

                List<Recipient> recipients = recevierAddresses.Select(address => new Recipient
                    {
                        ScriptPubKey = address.ScriptPubKey,
                        Amount = Money.COIN
                    })
                    .ToList();

                var transactionBuildContext = new TransactionBuildContext(
                    new WalletAccountReference("mywallet", "account 0"), recipients, "123456")
                    {
                        FeeType = FeeType.Medium,
                        MinConfirmations = 101
                    };

                Transaction transaction = xelsSender.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
                Assert.Equal(51, transaction.Outputs.Count);

                // Broadcast to the other node.
                xelsSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));

                // Wait for the trx's to arrive.
                TestHelper.WaitLoop(() => xelsReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.WaitLoop(() => xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

                Assert.Equal(Money.COIN * 50, xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount));
                Assert.Null(xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);  

                // Generate new blocks so the trx is confirmed.
                xelsSender.GenerateXelsWithMiner(1);

                // Wait for block repo for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));

                // Confirm trx's have been committed to the block.
                Assert.Equal(maturity + 52 , xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);
            }
        }

        [Fact]
        public void CanMineAndSendToAddress()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode xelsNodeSync = builder.CreateXelsPowNode();
                builder.StartAll();

                // Move a wallet file to the right folder and restart the wallet manager to take it into account.
                this.InitializeTestWallet(xelsNodeSync.FullNode.DataFolder.WalletPath);
                var walletManager = xelsNodeSync.FullNode.NodeService<IWalletManager>() as WalletManager;
                walletManager.Start();

                var rpc = xelsNodeSync.CreateRPCClient();
                rpc.SendCommand(NBitcoin.RPC.RPCOperations.generate, 10);
                Assert.Equal(10, rpc.GetBlockCount());

                var address = new Key().PubKey.GetAddress(rpc.Network);
                var tx = rpc.SendToAddress(address, Money.Coins(1.0m));
                Assert.NotNull(tx);
            }
        }

        [Fact]
        public void WalletCanReorg()
        {
            // this test has 4 parts:
            // send first transaction from one wallet to another and wait for it to be confirmed
            // send a second transaction and wait for it to be confirmed
            // connected to a longer chain that couse a reorg back so the second trasnaction is undone
            // mine the second transaction back in to the main chain

            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsSender = builder.CreateXelsPowNode();
                var xelsReceiver = builder.CreateXelsPowNode();
                var xelsReorg = builder.CreateXelsPowNode();

                builder.StartAll();
                xelsSender.NotInIBD();
                xelsReceiver.NotInIBD();
                xelsReorg.NotInIBD();

                // get a key from the wallet
                var mnemonic1 = xelsSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                var mnemonic2 = xelsReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = xelsSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = xelsSender.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                xelsSender.SetDummyMinerSecret(new BitcoinSecret(key, xelsSender.FullNode.Network));
                xelsReorg.SetDummyMinerSecret(new BitcoinSecret(key, xelsSender.FullNode.Network));

                var maturity = (int)xelsSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                xelsSender.GenerateXelsWithMiner(maturity + 15);

                var currentBestHeight = maturity + 15;

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));

                // the mining should add coins to the wallet
                var total = xelsSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * currentBestHeight * 50, total);

                // sync all nodes
                xelsReceiver.CreateRPCClient().AddNode(xelsSender.Endpoint, true);
                xelsReceiver.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                xelsSender.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsReorg));

                // Build Transaction 1
                // ====================
                // send coins to the receiver
                var sendto = xelsReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var transaction1 = xelsSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

                // broadcast to the other node
                xelsSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction1.ToHex()));

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => xelsReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(xelsReceiver.CreateRPCClient().GetRawTransaction(transaction1.GetHash(), false));
                TestHelper.WaitLoop(() => xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

                var receivetotal = xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // generate two new blocks so the trx is confirmed
                xelsSender.GenerateXelsWithMiner(1);
                var transaction1MinedHeight = currentBestHeight + 1;
                xelsSender.GenerateXelsWithMiner(1);
                currentBestHeight = currentBestHeight + 2;

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsReorg));
                Assert.Equal(currentBestHeight, xelsReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => transaction1MinedHeight == xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // Build Transaction 2
                // ====================
                // remove the reorg node
                xelsReceiver.CreateRPCClient().RemoveNode(xelsReorg.Endpoint);
                xelsSender.CreateRPCClient().RemoveNode(xelsReorg.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(xelsReorg));
                var forkblock = xelsReceiver.FullNode.Chain.Tip;

                // send more coins to the wallet
                sendto = xelsReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var transaction2 = xelsSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 10, FeeType.Medium, 101));
                xelsSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));
                // wait for the trx to arrive
                TestHelper.WaitLoop(() => xelsReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(xelsReceiver.CreateRPCClient().GetRawTransaction(transaction2.GetHash(), false));
                TestHelper.WaitLoop(() => xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());
                var newamount = xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 110, newamount);
                Assert.Contains(xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet"), b => b.Transaction.BlockHeight == null);

                // mine more blocks so its included in the chain

                xelsSender.GenerateXelsWithMiner(1);
                var transaction2MinedHeight = currentBestHeight + 1;
                xelsSender.GenerateXelsWithMiner(1);
                currentBestHeight = currentBestHeight + 2;
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                Assert.Equal(currentBestHeight, xelsReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));

                // create a reorg by mining on two different chains
                // ================================================
                // advance both chains, one chin is longer
                xelsSender.GenerateXelsWithMiner(2);
                xelsReorg.GenerateXelsWithMiner(10);
                currentBestHeight = forkblock.Height + 10;
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsReorg));

                // connect the reorg chain
                xelsReceiver.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                xelsSender.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                // wait for the chains to catch up
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsReorg));
                Assert.Equal(currentBestHeight, xelsReceiver.FullNode.Chain.Tip.Height);

                // ensure wallet reorg complete
                TestHelper.WaitLoop(() => xelsReceiver.FullNode.WalletManager().WalletTipHash == xelsReorg.CreateRPCClient().GetBestBlockHash());
                // check the wallet amount was rolled back
                var newtotal = xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(receivetotal, newtotal);
                TestHelper.WaitLoop(() => maturity + 16 == xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // ReBuild Transaction 2
                // ====================
                // After the reorg transaction2 was returned back to mempool
                xelsSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));

                TestHelper.WaitLoop(() => xelsReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                // mine the transaction again
                xelsSender.GenerateXelsWithMiner(1);
                transaction2MinedHeight = currentBestHeight + 1;
                xelsSender.GenerateXelsWithMiner(1);
                currentBestHeight = currentBestHeight + 2;

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsReorg));
                Assert.Equal(currentBestHeight, xelsReceiver.FullNode.Chain.Tip.Height);
                var newsecondamount = xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(newamount, newsecondamount);
                TestHelper.WaitLoop(() => xelsReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));
            }
        }

        [Fact]
        public void Given__TheNodeHadAReorg_And_WalletTipIsBehindConsensusTip__When__ANewBlockArrives__Then__WalletCanRecover()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsSender = builder.CreateXelsPowNode();
                var xelsReceiver = builder.CreateXelsPowNode();
                var xelsReorg = builder.CreateXelsPowNode();

                builder.StartAll();
                xelsSender.NotInIBD();
                xelsReceiver.NotInIBD();
                xelsReorg.NotInIBD();

                xelsSender.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsSender.FullNode.Network));
                xelsReorg.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsReorg.FullNode.Network));

                xelsSender.GenerateXelsWithMiner(10);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));

                //// sync all nodes
                xelsReceiver.CreateRPCClient().AddNode(xelsSender.Endpoint, true);
                xelsReceiver.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                xelsSender.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsReorg));

                // remove the reorg node
                xelsReceiver.CreateRPCClient().RemoveNode(xelsReorg.Endpoint);
                xelsSender.CreateRPCClient().RemoveNode(xelsReorg.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(xelsReorg));

                // create a reorg by mining on two different chains
                // ================================================
                // advance both chains, one chin is longer
                xelsSender.GenerateXelsWithMiner(2);
                xelsReorg.GenerateXelsWithMiner(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsReorg));

                // rewind the wallet in the xelsReceiver node
                (xelsReceiver.FullNode.NodeService<IWalletSyncManager>() as WalletSyncManager).SyncFromHeight(5);

                // connect the reorg chain
                xelsReceiver.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                xelsSender.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                // wait for the chains to catch up
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsReorg));
                Assert.Equal(20, xelsReceiver.FullNode.Chain.Tip.Height);

                xelsSender.GenerateXelsWithMiner(5);

                TestHelper.TriggerSync(xelsReceiver);
                TestHelper.TriggerSync(xelsSender);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                Assert.Equal(25, xelsReceiver.FullNode.Chain.Tip.Height);
            }
        }

        [Fact]
        public void Given__TheNodeHadAReorg_And_ConensusTipIsdifferentFromWalletTip__When__ANewBlockArrives__Then__WalletCanRecover()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsSender = builder.CreateXelsPowNode();
                var xelsReceiver = builder.CreateXelsPowNode();
                var xelsReorg = builder.CreateXelsPowNode();

                builder.StartAll();
                xelsSender.NotInIBD();
                xelsReceiver.NotInIBD();
                xelsReorg.NotInIBD();

                xelsSender.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsSender.FullNode.Network));
                xelsReorg.SetDummyMinerSecret(new BitcoinSecret(new Key(), xelsReorg.FullNode.Network));

                xelsSender.GenerateXelsWithMiner(10);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));

                //// sync all nodes
                xelsReceiver.CreateRPCClient().AddNode(xelsSender.Endpoint, true);
                xelsReceiver.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                xelsSender.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsReorg));

                // remove the reorg node and wait for node to be disconnected
                xelsReceiver.CreateRPCClient().RemoveNode(xelsReorg.Endpoint);
                xelsSender.CreateRPCClient().RemoveNode(xelsReorg.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(xelsReorg));

                // create a reorg by mining on two different chains
                // ================================================
                // advance both chains, one chin is longer
                xelsSender.GenerateXelsWithMiner(2);
                xelsReorg.GenerateXelsWithMiner(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsSender));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsReorg));

                // connect the reorg chain
                xelsReceiver.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                xelsSender.CreateRPCClient().AddNode(xelsReorg.Endpoint, true);
                // wait for the chains to catch up
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsReorg));
                Assert.Equal(20, xelsReceiver.FullNode.Chain.Tip.Height);

                // rewind the wallet in the xelsReceiver node
                (xelsReceiver.FullNode.NodeService<IWalletSyncManager>() as WalletSyncManager).SyncFromHeight(10);

                xelsSender.GenerateXelsWithMiner(5);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(xelsReceiver, xelsSender));
                Assert.Equal(25, xelsReceiver.FullNode.Chain.Tip.Height);
            }
        }

        [Fact]
        public void WalletCanCatchupWithBestChain()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsminer = builder.CreateXelsPowNode();

                builder.StartAll();
                xelsminer.NotInIBD();

                // get a key from the wallet
                var mnemonic = xelsminer.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic.Words.Length);
                var addr = xelsminer.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = xelsminer.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                xelsminer.SetDummyMinerSecret(key.GetBitcoinSecret(xelsminer.FullNode.Network));
                xelsminer.GenerateXels(10);
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsminer));

                // push the wallet back
                xelsminer.FullNode.Services.ServiceProvider.GetService<IWalletSyncManager>().SyncFromHeight(5);

                xelsminer.GenerateXels(5);

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsminer));
            }
        }

        [Fact]
        public void WalletCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var xelsNodeSync = builder.CreateXelsPowNode();
                builder.StartAll();
                xelsNodeSync.NotInIBD();

                // get a key from the wallet
                var mnemonic = xelsNodeSync.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic.Words.Length);
                var addr = xelsNodeSync.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = xelsNodeSync.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                xelsNodeSync.SetDummyMinerSecret(key.GetBitcoinSecret(xelsNodeSync.FullNode.Network));
                xelsNodeSync.GenerateXels(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(xelsNodeSync));

                // set the tip of best chain some blocks in the apst
                xelsNodeSync.FullNode.Chain.SetTip(xelsNodeSync.FullNode.Chain.GetBlock(xelsNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                xelsNodeSync.FullNode.Dispose();

                var newNodeInstance = builder.CloneXelsNode(xelsNodeSync);

                // load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // check that store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.WalletManager().WalletTipHash);
            }
        }

        public static TransactionBuildContext CreateContext(WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        /// <summary>
        /// Copies the test wallet into data folder for node if it isnt' already present.
        /// </summary>
        /// <param name="path">The path of the folder to move the wallet to.</param>
        private void InitializeTestWallet(string path)
        {
            string testWalletPath = Path.Combine(path, "test.wallet.json");
            if (!File.Exists(testWalletPath))
                File.Copy("Data/test.wallet.json", testWalletPath);
        }
    }
}