using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Xels.Bitcoin.Features.Consensus;
using Xels.Bitcoin.Features.Wallet;
using Xels.Bitcoin.Features.Wallet.Controllers;
using Xels.Bitcoin.Features.Wallet.Models;
using Xels.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Xels.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfWorkSpendingSpecification
    {
        private const string SendingWalletName = "sending wallet";
        private const string ReceivingWalletName = "receiving wallet";
        private const string WalletPassword = "123456";
        private const string AccountName = "account 0";
        private NodeBuilder nodeBuilder;
        private CoreNode sendingXelsBitcoinNode;
        private CoreNode receivingXelsBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;
        private Transaction lastTransaction;
        private int totalMinedBlocks;

        // NOTE: This constructor is allows test steps names to be logged
        public ProofOfWorkSpendingSpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void a_sending_and_receiving_xels_bitcoin_node_and_wallet()
        {
            this.sendingXelsBitcoinNode = this.nodeBuilder.CreateXelsPowNode();
            this.receivingXelsBitcoinNode = this.nodeBuilder.CreateXelsPowNode();

            this.nodeBuilder.StartAll();
            this.sendingXelsBitcoinNode.NotInIBD();
            this.receivingXelsBitcoinNode.NotInIBD();

            this.sendingXelsBitcoinNode.CreateRPCClient().AddNode(this.receivingXelsBitcoinNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.receivingXelsBitcoinNode, this.sendingXelsBitcoinNode));

            this.sendingXelsBitcoinNode.FullNode.WalletManager().CreateWallet(WalletPassword, SendingWalletName);
            this.receivingXelsBitcoinNode.FullNode.WalletManager().CreateWallet(WalletPassword, ReceivingWalletName);

            this.coinbaseMaturity = (int)this.sendingXelsBitcoinNode.FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
        }

        private void a_block_is_mined_creating_spendable_coins()
        {
            this.MineBlocks(1, this.sendingXelsBitcoinNode);
        }

        private void more_blocks_mined_to_just_BEFORE_maturity_of_original_block()
        {
            this.MineBlocks(this.coinbaseMaturity - 1, this.sendingXelsBitcoinNode);
        }

        private void more_blocks_mined_to_just_AFTER_maturity_of_original_block()
        {
            this.MineBlocks(this.coinbaseMaturity, this.sendingXelsBitcoinNode);
        }

        private void spending_the_coins_from_original_block()
        {
            var sendtoAddress = this.receivingXelsBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(ReceivingWalletName, AccountName), 2).ElementAt(1);

            try
            {
                this.lastTransaction = this.sendingXelsBitcoinNode.FullNode.WalletTransactionHandler().BuildTransaction(
                    CreateTransactionBuildContext(new WalletAccountReference(SendingWalletName, AccountName), WalletPassword, sendtoAddress.ScriptPubKey,
                        Money.COIN * 1, FeeType.Medium, 101));

                this.sendingXelsBitcoinNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.lastTransaction.ToHex()));
            }
            catch (Exception exception)
            {
                this.caughtException = exception;
            }
        }

        private void the_transaction_is_rejected_from_the_mempool()
        {
            this.caughtException.Should().BeOfType<WalletException>();

            var walletException = (WalletException)this.caughtException;
            walletException.Message.Should().Be("No spendable transactions found.");

            this.ResetCaughtException();
        }

        private void the_transaction_is_put_in_the_mempool()
        {
            var tx = this.sendingXelsBitcoinNode.FullNode.MempoolManager().GetTransaction(this.lastTransaction.GetHash()).GetAwaiter().GetResult();
            tx.GetHash().Should().Be(this.lastTransaction.GetHash());
            this.caughtException.Should().BeNull();
        }

        public static TransactionBuildContext CreateTransactionBuildContext(WalletAccountReference accountReference, string password, Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        private void MineBlocks(int blockCount, CoreNode node)
        {
            var address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(SendingWalletName, AccountName));
            var wallet = node.FullNode.WalletManager().GetWalletByName(SendingWalletName);
            var extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(WalletPassword, address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            node.GenerateXelsWithMiner(blockCount);
            this.totalMinedBlocks = this.totalMinedBlocks + blockCount;

            this.sendingXelsBitcoinNode.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(SendingWalletName)
                .Sum(s => s.Transaction.Amount)
                .Should().Be(Money.COIN * this.totalMinedBlocks * 50);

            WaitForBlockStoreToSync(node);
        }

        private void ResetCaughtException()
        {
            this.caughtException = null;
        }

        private void WaitForBlockStoreToSync(CoreNode node)
        {
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(node));
        }
    }
}